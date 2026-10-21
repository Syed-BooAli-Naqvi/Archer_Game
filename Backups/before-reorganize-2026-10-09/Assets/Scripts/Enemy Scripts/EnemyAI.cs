using System.Collections.Generic;
using UnityEngine;

public class EnemyAI : MonoBehaviour
{
    public enum State
    {
        Approach,
        Attack,
        Idle
    }

    public enum StatusEffect
    {
        None,
        Freeze,
        Fire
    }

    public EnemyHealth myHealth;

    [Header("Target")]
    public Transform target;
    public string targetTag = "Player";

    [Header("Movement")]
    public float moveSpeed = 2.5f;
    public float stoppingDistance = 1.5f;

    [Header("Spacing")]
    [Tooltip("Enemies are never allowed closer than this to each other")]
    public float minSpacing = 0.9f;

    [Tooltip("How many times per frame overlaps are re-resolved (more = stricter in crowds)")]
    public int separationIterations = 3;

    [Header("Attack")]
    public float attackRange = 1.8f;
    public float attackDamage = 10f;
    public float attackCooldown = 1.2f;

    [Header("Animators")]
    public List<Animator> animators = new List<Animator>();
    public string attackTrigger = "Sword Attack";

    [Header("Enemy Visuals")]
    public GameObject normalEnemy;
    public GameObject frozenEnemy;
    public GameObject fireEnemy;

    [Header("Status Settings")]
    [Tooltip("Speed multiplier while frozen or on fire")]
    public float statusSpeedMultiplier = 0.5f;

    [Tooltip("Delay between explode particle and Die()")]
    public float explodeDieDelay = 0.4f;

    [Header("Freeze Settings")]
    public ParticleSystem freezeExplode;

    [Header("Fire Settings")]
    public ParticleSystem fireExplode;

    private State state = State.Approach;
    public StatusEffect activeEffect = StatusEffect.None;

    private float lastAttackTime = -999f;

    private ArcherPlayerHealth targetHealth;

    private bool isDying = false;

    private static readonly int IdleHash = Animator.StringToHash("Idle");
    private static readonly int RunningHash = Animator.StringToHash("Running");
    private int currentLocomotion;

    // =========================================================
    // SHARED ENEMY REGISTRY (used for spacing)
    // =========================================================

    private static readonly List<EnemyAI> activeEnemies =
        new List<EnemyAI>();

    // Enemies currently allowed to attack (capped by DifficultySettings.maxSimultaneousAttackers).
    private static readonly HashSet<EnemyAI> attackers = new HashSet<EnemyAI>();

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        activeEnemies.Clear();
        attackers.Clear();
    }

    private void OnEnable()
    {
        if (!activeEnemies.Contains(this))
            activeEnemies.Add(this);
    }

    private void OnDisable()
    {
        activeEnemies.Remove(this);
        attackers.Remove(this);
    }

    private void Start()
    {
        DifficultySettings.ApplyParticleBudget(gameObject);
        SetupAnimators();

        if (target == null)
        {
            GameObject found =
                GameObject.FindGameObjectWithTag(targetTag);

            if (found != null)
                target = found.transform;
        }

        if (target != null)
            targetHealth =
                target.GetComponent<ArcherPlayerHealth>();

        SetNormalEnemy();
    }

    private void SetupAnimators()
    {
        // If no animators were assigned in Inspector,
        // automatically get all animators from this object and children.
        if (animators == null)
            animators = new List<Animator>();

        if (animators.Count == 0)
        {
            Animator[] foundAnimators =
                GetComponentsInChildren<Animator>(true);

            animators.AddRange(foundAnimators);
        }

        // Remove null references
        animators.RemoveAll(animator => animator == null);
    }

    private void Update()
    {
        // Nothing happens while game is stopped
        if (!ArcherGameState.IsGameRunning)
            return;

        // Playing explode effect, waiting for Die()
        if (isDying)
            return;

        if (target == null)
            return;

        float dist =
            Vector3.Distance(
                transform.position,
                target.position
            );

        Vector3 startPosition = transform.position;

        switch (state)
        {
            case State.Approach:

                if (dist <= AttackRange && CanJoinAttack())
                {
                    state = State.Attack;
                    attackers.Add(this);
                    break;
                }

                // Attack slots are full: wait just outside reach instead of pushing in.
                if (dist <= AttackRange + 1.2f && !CanJoinAttack())
                {
                    FaceTarget();
                    break;
                }

                MoveTowardTarget();

                if (!DifficultySettings.Current.SpacingActive)
                    SetLocomotion(RunningHash);
                break;

            case State.Attack:

                if (dist > AttackRange * 1.1f)
                {
                    state = State.Approach;
                    attackers.Remove(this);
                    break;
                }

                FaceTarget();
                TryAttack();

                if (!DifficultySettings.Current.SpacingActive)
                    SetLocomotion(IdleHash);
                break;

            case State.Idle:
                break;
        }

        ResolveSpacing();
        KeepClearOfBossAndPlayer();
        KeepInFrontOfTarget();

        if (DifficultySettings.Current.SpacingActive)
            UpdateLocomotionFromMovement(startPosition);
    }

    // Smoothed ground speed, so blocked enemies in a crowd stand idle instead of running in place.
    private float smoothedSpeed;

    private void UpdateLocomotionFromMovement(Vector3 startPosition)
    {
        if (Time.deltaTime <= 0f)
            return;

        Vector3 moved = transform.position - startPosition;
        moved.y = 0f;

        smoothedSpeed = Mathf.Lerp(smoothedSpeed, moved.magnitude / Time.deltaTime, 10f * Time.deltaTime);

        float runAbove = moveSpeed * 0.45f;
        float idleBelow = moveSpeed * 0.2f;

        if (smoothedSpeed > runAbove)
            SetLocomotion(RunningHash);
        else if (smoothedSpeed < idleBelow)
            SetLocomotion(IdleHash);
    }

    private bool CanJoinAttack()
    {
        DifficultySettings settings = DifficultySettings.Current;

        if (!settings.SpacingActive || settings.maxSimultaneousAttackers <= 0)
            return true;

        attackers.RemoveWhere(a => a == null || a.isDying || (a.myHealth != null && !EnemyHealth.Alive.Contains(a.myHealth)));
        return attackers.Contains(this) || attackers.Count < settings.maxSimultaneousAttackers;
    }

    /// <summary>Attack reach; with better spacing it covers the gap enemies keep from the player.</summary>
    private float AttackRange
    {
        get
        {
            DifficultySettings settings = DifficultySettings.Current;
            return settings.SpacingActive ? Mathf.Max(attackRange, settings.playerClearance + 0.15f) : attackRange;
        }
    }

    // Runs after every enemy's Update, so pushes from neighbours this frame can't move it back off screen.
    private void LateUpdate()
    {
        if (ArcherGameState.IsGameRunning)
            KeepOnScreen();
    }

    /// <summary>Holds the enemy inside the left/right screen edges so big crowds never spill out of view.</summary>
    private void KeepOnScreen()
    {
        DifficultySettings settings = DifficultySettings.Current;
        Camera cam = Camera.main;

        if (!settings.KeepEnemiesOnScreenActive || cam == null)
            return;

        Vector3 viewport = cam.WorldToViewportPoint(transform.position);

        // Only while on screen vertically (waves walking in from the top are left alone).
        if (viewport.z <= 0f || viewport.y < 0f || viewport.y > 1f)
            return;

        float margin = settings.screenEdgeMargin;
        float clampedX = Mathf.Clamp(viewport.x, margin, 1f - margin);

        if (Mathf.Approximately(clampedX, viewport.x))
            return;

        Ray ray = cam.ViewportPointToRay(new Vector3(clampedX, viewport.y, 0f));
        Plane ground = new Plane(Vector3.up, transform.position);

        if (ground.Raycast(ray, out float distance))
        {
            Vector3 position = transform.position;
            position.x = ray.GetPoint(distance).x;
            transform.position = position;
        }
    }

    /// <summary>Stops enemies from walking into the boss or standing inside the player model.</summary>
    private void KeepClearOfBossAndPlayer()
    {
        DifficultySettings settings = DifficultySettings.Current;

        if (!settings.SpacingActive)
            return;

        foreach (BossAI boss in BossAI.Active)
            PushOutOf(boss.transform.position, settings.bossClearance, false);

        PushOutOf(target.position, settings.playerClearance, true);
    }

    private void PushOutOf(Vector3 center, float radius, bool stayInFront)
    {
        Vector3 offset = transform.position - center;
        offset.y = 0f;

        if (offset.sqrMagnitude >= radius * radius)
            return;

        Vector3 direction = offset.sqrMagnitude > 0.0001f ? offset.normalized : Vector3.forward;

        // Around the player, keep enemies in a front cone (about +-37 degrees) where arrows can reach them.
        if (stayInFront && direction.z < 0.8f)
        {
            float side = Mathf.Abs(direction.x) > 0.001f ? Mathf.Sign(direction.x) : (Random.value < 0.5f ? -1f : 1f);
            direction = new Vector3(side * 0.6f, 0f, 0.8f);
        }

        Vector3 position = center + direction * radius;
        position.y = transform.position.y;
        transform.position = position;
    }

    /// <summary>
    /// Arrows only fly forward, so an enemy that gets behind the player can't be hit.
    /// Hold every enemy on a front line just ahead of the player instead.
    /// </summary>
    private void KeepInFrontOfTarget()
    {
        DifficultySettings settings = DifficultySettings.Current;

        if (!settings.KeepEnemiesInFrontActive)
            return;

        float minZ = target.position.z + settings.frontLineOffset;

        if (transform.position.z < minZ)
        {
            Vector3 position = transform.position;
            position.z = minZ;
            transform.position = position;
        }
    }

    private float CurrentSpeed()
    {
        // Frozen, fire, or both: always the same multiplier
        if (activeEffect != StatusEffect.None)
            return moveSpeed * statusSpeedMultiplier;

        return moveSpeed;
    }

    private void MoveTowardTarget()
    {
        Vector3 dir =
            target.position -
            transform.position;

        dir.y = 0f;

        if (dir.sqrMagnitude <= 0.001f)
            return;

        dir.Normalize();

        transform.position +=
            dir *
            CurrentSpeed() *
            Time.deltaTime;

        transform.rotation =
            Quaternion.LookRotation(dir);
    }

    private void FaceTarget()
    {
        Vector3 dir =
            target.position -
            transform.position;

        dir.y = 0f;

        if (dir.sqrMagnitude > 0.001f)
        {
            transform.rotation =
                Quaternion.LookRotation(dir);
        }
    }

    // =========================================================
    // SPACING
    // =========================================================

    /// <summary>
    /// Pushes this enemy and any too-close neighbours apart on the XZ plane
    /// until nobody is closer than minSpacing.
    /// Attacking enemies hold their ground; approaching enemies yield to them.
    /// </summary>
    private void ResolveSpacing()
    {
        if (minSpacing <= 0f)
            return;

        float minSqr = minSpacing * minSpacing;

        int iterations = Mathf.Max(1, separationIterations);

        for (int it = 0; it < iterations; it++)
        {
            bool movedAny = false;

            for (int i = 0; i < activeEnemies.Count; i++)
            {
                EnemyAI other = activeEnemies[i];

                if (other == null || other == this)
                    continue;

                Vector3 delta =
                    transform.position -
                    other.transform.position;

                delta.y = 0f;

                float sqr = delta.sqrMagnitude;

                if (sqr >= minSqr)
                    continue;

                float distance = Mathf.Sqrt(sqr);

                Vector3 pushDir;

                if (distance < 0.0001f)
                {
                    // Exactly on top of each other: pick a random direction
                    Vector2 r = Random.insideUnitCircle.normalized;
                    pushDir = new Vector3(r.x, 0f, r.y);
                }
                else
                {
                    pushDir = delta / distance;
                }

                float overlap = minSpacing - distance;

                // Attacking enemies are anchored, others yield
                bool selfAnchored = state == State.Attack;
                bool otherAnchored = other.state == State.Attack;

                float selfShare;
                float otherShare;

                if (selfAnchored && !otherAnchored)
                {
                    selfShare = 0f;
                    otherShare = 1f;
                }
                else if (!selfAnchored && otherAnchored)
                {
                    selfShare = 1f;
                    otherShare = 0f;
                }
                else
                {
                    selfShare = 0.5f;
                    otherShare = 0.5f;
                }

                transform.position +=
                    pushDir * overlap * selfShare;

                other.transform.position -=
                    pushDir * overlap * otherShare;

                movedAny = true;
            }

            if (!movedAny)
                break;
        }
    }

    private void TryAttack()
    {
        if (Time.time - lastAttackTime < attackCooldown)
            return;

        lastAttackTime = Time.time;

        PlayAttackAnimation();

        if (targetHealth != null)
            targetHealth.TakeDamage(attackDamage);
    }

    private void PlayAttackAnimation()
    {
        if (animators == null)
            return;

        for (int i = 0; i < animators.Count; i++)
        {
            // Hidden frozen/fire variants have inactive animators; skip them.
            if (animators[i] == null || !animators[i].isActiveAndEnabled)
                continue;

            animators[i].SetTrigger(attackTrigger);
        }
    }

    /// <summary>Idle while standing to attack, Running while moving (base layer).</summary>
    private void SetLocomotion(int stateHash)
    {
        if (currentLocomotion == stateHash)
            return;

        currentLocomotion = stateHash;

        for (int i = 0; i < animators.Count; i++)
        {
            Animator animator = animators[i];

            if (animator == null || !animator.isActiveAndEnabled || !animator.HasState(0, stateHash))
                continue;

            animator.CrossFadeInFixedTime(stateHash, 0.15f, 0);
        }
    }

    // =========================================================
    // FREEZE ENEMY
    // =========================================================

    public void FreezeEnemy(float damage)
    {
        if (!ArcherGameState.IsGameRunning)
            return;

        if (isDying)
            return;

        if (activeEffect == StatusEffect.Freeze)
            return;

        activeEffect = StatusEffect.Freeze;

        // Normal OFF
        if (normalEnemy != null)
            normalEnemy.SetActive(false);

        // Fire OFF
        if (fireEnemy != null)
            fireEnemy.SetActive(false);

        // Frozen ON
        if (frozenEnemy != null)
            frozenEnemy.SetActive(true);

        currentLocomotion = 0;
    }

    // =========================================================
    // FIRE ENEMY
    // =========================================================

    public void FireEnemy(float damage)
    {
        if (!ArcherGameState.IsGameRunning)
            return;

        if (isDying)
            return;

        if (activeEffect == StatusEffect.Fire)
            return;

        activeEffect = StatusEffect.Fire;

        // Normal OFF
        if (normalEnemy != null)
            normalEnemy.SetActive(false);

        // Frozen OFF
        if (frozenEnemy != null)
            frozenEnemy.SetActive(false);

        // Fire ON
        if (fireEnemy != null)
            fireEnemy.SetActive(true);

        currentLocomotion = 0;
    }

    // =========================================================
    // DEATH
    // =========================================================

    /// <summary>
    /// Called by EnemyHealth when health reaches 0.
    /// Returns true if a frozen/burning enemy will play its
    /// explode particle and then die.
    /// Returns false if the enemy has no status effect and
    /// EnemyHealth should die immediately.
    /// </summary>
    public bool TryPlayStatusDeath()
    {
        if (isDying)
            return true;

        if (activeEffect == StatusEffect.None)
            return false;

        isDying = true;

        if (activeEffect == StatusEffect.Freeze)
        {
            // if (frozenEnemy != null)
            //     frozenEnemy.SetActive(false);

            if (freezeExplode != null)
                freezeExplode.Play();
        }
        else if (activeEffect == StatusEffect.Fire)
        {
            // if (fireEnemy != null)
            //     fireEnemy.SetActive(false);

            if (fireExplode != null)
                fireExplode.Play();
        }

        Die();

        return true;
    }

    public void Die()
    {
        if (myHealth != null)
            myHealth.Die();
    }

    private void SetNormalEnemy()
    {
        if (normalEnemy != null)
            normalEnemy.SetActive(true);

        if (frozenEnemy != null)
            frozenEnemy.SetActive(false);

        if (fireEnemy != null)
            fireEnemy.SetActive(false);

        activeEffect = StatusEffect.None;
    }

    public bool IsFrozen()
    {
        return activeEffect == StatusEffect.Freeze;
    }
}