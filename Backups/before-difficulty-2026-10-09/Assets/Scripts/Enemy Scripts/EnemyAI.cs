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

    // =========================================================
    // SHARED ENEMY REGISTRY (used for spacing)
    // =========================================================

    private static readonly List<EnemyAI> activeEnemies =
        new List<EnemyAI>();

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        activeEnemies.Clear();
    }

    private void OnEnable()
    {
        if (!activeEnemies.Contains(this))
            activeEnemies.Add(this);
    }

    private void OnDisable()
    {
        activeEnemies.Remove(this);
    }

    private void Start()
    {
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

        switch (state)
        {
            case State.Approach:

                if (dist <= attackRange)
                {
                    state = State.Attack;
                    break;
                }

                MoveTowardTarget();
                break;

            case State.Attack:

                if (dist > attackRange * 1.1f)
                {
                    state = State.Approach;
                    break;
                }

                FaceTarget();
                TryAttack();
                break;

            case State.Idle:
                break;
        }

        ResolveSpacing();
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
            if (animators[i] == null)
                continue;

            animators[i].SetTrigger(attackTrigger);
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