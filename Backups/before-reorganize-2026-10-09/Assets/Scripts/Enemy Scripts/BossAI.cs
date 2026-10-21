using System.Collections.Generic;
using UnityEngine;

public class BossAI : MonoBehaviour
{
    private enum State
    {
        Approach,
        Attack,
        Idle
    }

    public BossHealth myHealth;

    [Header("Target")]
    public Transform target;
    public string targetTag = "Player";

    [Header("Movement")]
    public float moveSpeed = 2f;
    public float stoppingDistance = 2f;

    [Header("Attack")]
    public float attackRange = 2.5f;
    public float attackDamage = 25f;
    public float attackCooldown = 1.5f;

    [Header("Animators")]
    public List<Animator> animators = new List<Animator>();
    public string attackTrigger = "Sword Attack";

    private State state = State.Approach;

    private float lastAttackTime = -999f;

    private ArcherPlayerHealth targetHealth;

    private bool isDying = false;

    // ---------- Charge attack (DifficultySettings.bossChargeAttacks) ----------
    private enum ChargeState
    {
        Approach,
        Hold,
        Telegraph,
        Charging,
        Recover
    }

    private ChargeState chargeState = ChargeState.Approach;
    private float chargeTimer;
    private Vector3 chargeTarget;
    private bool chargeHit;
    private GameObject chargeMarker;
    private string currentLocomotion;
    private Vector3 chargeLanding;

    // Bosses currently in the scene (enemies keep their distance from them).
    private static readonly List<BossAI> active = new List<BossAI>();
    public static IReadOnlyList<BossAI> Active => active;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        active.Clear();
    }

    private void OnEnable()
    {
        active.Add(this);
    }

    // ---------- Minions (DifficultySettings.bossMinions) ----------
    private float nextMinionTime = -1f;

    /// <summary>True while the boss is warning where it is about to charge.</summary>
    public bool IsTelegraphing => chargeState == ChargeState.Telegraph;

    /// <summary>Where the current / next charge will land.</summary>
    public Vector3 ChargeTarget => chargeTarget;

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
    }

    private void SetupAnimators()
    {
        if (animators == null)
            animators = new List<Animator>();

        if (animators.Count == 0)
        {
            Animator[] foundAnimators =
                GetComponentsInChildren<Animator>(true);

            animators.AddRange(foundAnimators);
        }

        animators.RemoveAll(animator => animator == null);
    }

    private void Update()
    {
        if (!ArcherGameState.IsGameRunning)
            return;

        if (isDying)
            return;

        if (target == null)
            return;

        DifficultySettings settings = DifficultySettings.Current;

        if (settings.BossMinionsActive)
            UpdateMinions(settings);

        if (settings.BossChargeActive)
        {
            UpdateCharge(settings);
            return;
        }

        float dist = Vector3.Distance(
            transform.position,
            target.position
        );

        switch (state)
        {
            case State.Approach:

                // Stop approaching once stopping distance is reached.
                if (dist <= stoppingDistance)
                {
                    state = State.Attack;
                    break;
                }

                MoveTowardTarget();
                break;

            case State.Attack:

                // If pushed too far away, approach again.
                if (dist > stoppingDistance + 0.25f)
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
    }

    private float CurrentSpeed()
    {
        return moveSpeed;
    }

    private void MoveTowardTarget()
    {
        Vector3 dir =
            target.position -
            transform.position;

        dir.y = 0f;

        float distance = dir.magnitude;

        // Don't move closer than stopping distance.
        if (distance <= stoppingDistance)
        {
            state = State.Attack;
            return;
        }

        if (dir.sqrMagnitude <= 0.001f)
            return;

        dir.Normalize();

        float moveAmount =
            CurrentSpeed() *
            Time.deltaTime;

        // Prevent overshooting the stopping distance.
        float allowedDistance =
            distance - stoppingDistance;

        moveAmount =
            Mathf.Min(moveAmount, allowedDistance);

        transform.position +=
            dir * moveAmount;

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

    private void TryAttack()
    {
        if (Time.time - lastAttackTime < attackCooldown)
            return;

        lastAttackTime = Time.time;

        PlayAttackAnimation();

        if (targetHealth != null)
        {
            targetHealth.TakeDamage(attackDamage);
            PlayHitSound();
        }
    }

    private void PlayAttackAnimation()
    {
        if (animators == null)
            return;

        for (int i = 0; i < animators.Count; i++)
        {
            if (animators[i] == null || !animators[i].isActiveAndEnabled)
                continue;

            animators[i].SetTrigger(attackTrigger);
        }
    }

    private void PlayHitSound()
    {
        if (SoundManager.Instance != null)
            SoundManager.Instance.PlaySound(SoundName.BossHit);
    }

    // =========================================================
    // CHARGE ATTACK
    // =========================================================

    private void UpdateCharge(DifficultySettings settings)
    {
        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;
        float dist = toTarget.magnitude;

        switch (chargeState)
        {
            case ChargeState.Approach:
                if (dist <= settings.bossHoldDistance)
                {
                    chargeState = ChargeState.Hold;
                    chargeTimer = settings.chargeInterval * 0.5f;
                    break;
                }

                MoveFlat(toTarget.normalized, Mathf.Min(CurrentSpeed() * Time.deltaTime, dist - settings.bossHoldDistance));
                FaceDirection(toTarget);
                SetLocomotion("Running");
                break;

            case ChargeState.Hold:
                FaceDirection(toTarget);
                SetLocomotion("Idle");

                if (dist > settings.bossHoldDistance + 1f)
                {
                    chargeState = ChargeState.Approach;
                    break;
                }

                chargeTimer -= Time.deltaTime;

                if (chargeTimer <= 0f)
                    StartTelegraph(settings);
                break;

            case ChargeState.Telegraph:
                FaceDirection(chargeTarget - transform.position);
                chargeTimer -= Time.deltaTime;

                if (chargeMarker != null)
                {
                    // Pulse faster as the charge gets closer.
                    float pulse = 1f + 0.12f * Mathf.Sin(Time.time * Mathf.Lerp(30f, 10f, chargeTimer / settings.chargeTelegraphTime));
                    float size = settings.chargeHitRadius * 2f * pulse;
                    chargeMarker.transform.localScale = new Vector3(size, 0.01f, size);

                    // Fade in as the charge gets closer.
                    float urgency = 1f - Mathf.Clamp01(chargeTimer / settings.chargeTelegraphTime);
                    chargeMarker.GetComponent<Renderer>().material.color = new Color(1f, 0f, 0f, Mathf.Lerp(0.2f, 0.5f, urgency));
                }

                if (chargeTimer <= 0f)
                {
                    chargeState = ChargeState.Charging;
                    chargeHit = false;
                    SetLocomotion("Running");

                    // Stop short of the marked spot so the boss doesn't run into the player model.
                    chargeLanding = chargeTarget;

                    if (settings.SpacingActive)
                    {
                        Vector3 approach = chargeTarget - transform.position;
                        approach.y = 0f;
                        float stop = Mathf.Min(settings.chargeStopShort, approach.magnitude);
                        chargeLanding = chargeTarget - approach.normalized * stop;
                    }
                }
                break;

            case ChargeState.Charging:
                Vector3 toLanding = chargeLanding - transform.position;
                toLanding.y = 0f;
                float step = settings.chargeSpeed * Time.deltaTime;

                MoveFlat(toLanding.normalized, Mathf.Min(step, toLanding.magnitude));

                Vector3 toPlayer = target.position - transform.position;
                toPlayer.y = 0f;

                // Original rule: hit when the boss runs into the player.
                // With better spacing: hit when the player is still inside the marker on impact.
                bool landing = toLanding.magnitude <= step;
                Vector3 playerToMarker = target.position - chargeTarget;
                playerToMarker.y = 0f;

                bool hits = settings.SpacingActive
                    ? landing && playerToMarker.magnitude <= settings.chargeHitRadius
                    : toPlayer.magnitude <= settings.chargeHitRadius;

                if (!chargeHit && hits)
                {
                    chargeHit = true;

                    if (targetHealth != null)
                    {
                        targetHealth.TakeDamage(settings.chargeDamage);
                        PlayHitSound();
                    }
                }

                if (toLanding.magnitude <= step)
                {
                    chargeState = ChargeState.Recover;
                    chargeTimer = 0.6f;
                    HideChargeMarker();
                    currentLocomotion = null;
                    PlayAttackAnimation();
                }
                break;

            case ChargeState.Recover:
                if (chargeTimer > 0f)
                {
                    chargeTimer -= Time.deltaTime;
                    break;
                }

                // Back off to holding distance, then wait for the next charge.
                if (dist < settings.bossHoldDistance)
                {
                    Vector3 away = dist > 0.01f ? -toTarget.normalized : Vector3.forward;
                    MoveFlat(away, Mathf.Min(CurrentSpeed() * 3f * Time.deltaTime, settings.bossHoldDistance - dist));
                    FaceDirection(toTarget);
                    SetLocomotion("Running");
                }
                else
                {
                    chargeState = ChargeState.Hold;
                    chargeTimer = settings.chargeInterval;
                }
                break;
        }
    }

    /// <summary>Cross-fades every boss animator to a locomotion state, only when it changes.</summary>
    private void SetLocomotion(string stateName)
    {
        if (currentLocomotion == stateName)
            return;

        currentLocomotion = stateName;

        for (int i = 0; i < animators.Count; i++)
        {
            if (animators[i] != null)
                animators[i].CrossFadeInFixedTime(stateName, 0.15f, 0);
        }
    }

    private void StartTelegraph(DifficultySettings settings)
    {
        chargeState = ChargeState.Telegraph;
        chargeTimer = settings.chargeTelegraphTime;

        chargeTarget = target.position;
        chargeTarget.y = transform.position.y;

        if (chargeMarker == null)
            chargeMarker = CreateChargeMarker();

        chargeMarker.transform.position = new Vector3(chargeTarget.x, target.position.y + 0.05f, chargeTarget.z);
        chargeMarker.SetActive(true);
    }

    private static GameObject CreateChargeMarker()
    {
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        marker.name = "Boss Charge Marker";
        Destroy(marker.GetComponent<Collider>());

        Renderer renderer = marker.GetComponent<Renderer>();
        renderer.material = new Material(Shader.Find("Sprites/Default"));
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // Draw above the gate strips and ground sprites so the warning is never hidden.
        renderer.sortingOrder = 50;
        renderer.material.color = new Color(1f, 0f, 0f, 0.35f);

        return marker;
    }

    private void HideChargeMarker()
    {
        if (chargeMarker != null)
            chargeMarker.SetActive(false);
    }

    private void OnDisable()
    {
        active.Remove(this);
        HideChargeMarker();
    }

    private void OnDestroy()
    {
        if (chargeMarker != null)
            Destroy(chargeMarker);
    }

    private void MoveFlat(Vector3 direction, float amount)
    {
        if (amount <= 0f)
            return;

        transform.position += direction * amount;
    }

    private void FaceDirection(Vector3 direction)
    {
        direction.y = 0f;

        if (direction.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(direction);
    }

    // =========================================================
    // MINIONS
    // =========================================================

    private void UpdateMinions(DifficultySettings settings)
    {
        if (nextMinionTime < 0f)
        {
            nextMinionTime = Time.time + settings.minionInterval * 0.5f;
            return;
        }

        if (Time.time < nextMinionTime)
            return;

        nextMinionTime = Time.time + settings.minionInterval;

        GameObject prefab = EnemySpawner.FindRegularEnemyPrefab();

        if (prefab == null)
            return;

        float power = DifficultySettings.PlayerPower();
        int count = Mathf.Max(1, settings.minionCount);

        for (int i = 0; i < count; i++)
        {
            Vector2 offset = Random.insideUnitCircle * 2.5f;

            // Spawn in a ring around the boss rather than inside it.
            if (settings.SpacingActive)
                offset = Random.insideUnitCircle.normalized * (settings.bossClearance + Random.Range(0f, 1f));

            Vector3 position = transform.position + new Vector3(offset.x, 0f, offset.y);

            GameObject minion = Instantiate(prefab, position, Quaternion.identity);

            EnemyAI ai = minion.GetComponent<EnemyAI>();
            if (ai != null)
            {
                ai.target = target;

                if (settings.SpacingActive)
                    ai.minSpacing = Mathf.Max(ai.minSpacing, settings.enemySpacing);
            }

            EnemySpawner.ApplyHealthScaling(minion, power);
        }
    }

    // =========================================================
    // DEATH
    // =========================================================

    public void Die()
    {
        if (myHealth != null)
            myHealth.Die();
    }
}