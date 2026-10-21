using System.Collections.Generic;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    public PlatformMover platformMover;
    [Header("Prefab & Target")]
    public GameObject enemyPrefab;
    public Transform playerTarget;

    [Header("Spawn Settings")]
    public int enemyCount = 10;
    public float spawnRadius = 5f;
    public float minSpacing = 0.9f;

    [Header("Enemy Lifetime")]
    public float destroyAfterSeconds = 10f;

    [Header("Trigger Settings")]
    public float triggerDistance = 10f;
    public bool triggerOnce = true;

    private bool hasTriggered = false;

    public bool isBoss;

    // All spawners in the scene, used to find the next wave to pull in early.
    private static readonly List<EnemySpawner> spawners = new List<EnemySpawner>();
    private static float clearedSince = -1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        spawners.Clear();
        clearedSince = -1f;
    }

    // Time the current run started (for the fixed level length). Reset when a level's spawners load.
    private static float runStartTime = -1f;

    /// <summary>Seconds since the current run started (0 before it starts).</summary>
    public static float RunTime => runStartTime < 0f ? 0f : Time.time - runStartTime;

    private void OnEnable()
    {
        if (spawners.Count == 0)
        {
            runStartTime = -1f;
            bossStartDistance = -1f;
        }

        spawners.Add(this);

        // Helpers that live alongside the spawners (each creates itself once).
        DifficultySettings settings = DifficultySettings.Current;

        if (settings.TrickleActive && TrickleSpawner.Instance == null)
            new GameObject("Trickle Spawner").AddComponent<TrickleSpawner>();

        if (settings.CoinsActive && CoinTrail.Instance == null)
            new GameObject("Coin Trail").AddComponent<CoinTrail>();

        if (settings.BoosterActive && BoosterPickup.Instance == null)
            new GameObject("Booster Pickup").AddComponent<BoosterPickup>();

        if (LevelProgressBar.Instance == null)
            new GameObject("Level Progress Bar").AddComponent<LevelProgressBar>();
    }

    // ---------- shared helpers for TrickleSpawner / CoinTrail ----------

    public static Transform FindPlayerTarget()
    {
        foreach (EnemySpawner spawner in spawners)
            if (spawner.playerTarget != null)
                return spawner.playerTarget;

        return null;
    }

    public static PlatformMover FindPlatformMover()
    {
        foreach (EnemySpawner spawner in spawners)
            if (spawner.platformMover != null)
                return spawner.platformMover;

        return null;
    }

    /// <summary>Ground height enemies spawn at.</summary>
    public static float GroundY => spawners.Count > 0 ? spawners[0].transform.position.y : 0f;

    private static float bossStartDistance = -1f;

    /// <summary>
    /// How far through the level the player is, 0..1 (1 = the boss has arrived).
    /// Time-based with a fixed level length, otherwise based on the distance to the boss spawner.
    /// </summary>
    public static float LevelProgress
    {
        get
        {
            if (BossTriggered)
                return 1f;

            DifficultySettings settings = DifficultySettings.Current;
            if (settings.FixedLevelLengthActive)
                return Mathf.Clamp01(RunTime / Mathf.Max(1f, settings.levelLengthSeconds));

            foreach (EnemySpawner spawner in spawners)
            {
                if (!spawner.isBoss || spawner.playerTarget == null)
                    continue;

                float distance = Mathf.Abs(spawner.transform.position.z - spawner.playerTarget.position.z) - spawner.triggerDistance;
                if (bossStartDistance < 0f || runStartTime < 0f)
                    bossStartDistance = Mathf.Max(1f, distance);

                return Mathf.Clamp01(1f - distance / bossStartDistance);
            }

            return 0f;
        }
    }

    /// <summary>True once the boss wave has been triggered.</summary>
    public static bool BossTriggered
    {
        get
        {
            foreach (EnemySpawner spawner in spawners)
                if (spawner.isBoss && spawner.hasTriggered)
                    return true;

            return false;
        }
    }

    /// <summary>Spawns one regular enemy set up like a wave enemy (target, spacing, scaled HP).</summary>
    public static GameObject SpawnEnemy(GameObject prefab, Vector3 position, Transform target, float power)
    {
        DifficultySettings settings = DifficultySettings.Current;
        GameObject enemyGO = Instantiate(prefab, position, Quaternion.identity);

        EnemyAI ai = enemyGO.GetComponent<EnemyAI>();
        if (ai != null)
        {
            ai.target = target;
            if (settings.SpacingActive)
                ai.minSpacing = Mathf.Max(ai.minSpacing, settings.enemySpacing);
        }

        ApplyHealthScaling(enemyGO, power);
        return enemyGO;
    }

    private void OnDisable()
    {
        spawners.Remove(this);
    }

    private void Update()
    {
        // Game has not started yet
        if (!ArcherGameState.IsGameRunning)
            return;

        if (hasTriggered && triggerOnce)
            return;

        if (playerTarget == null)
            return;

        float dist = Vector3.Distance(
            transform.position,
            playerTarget.position
        );

        if (runStartTime < 0f)
            runStartTime = Time.time;

        DifficultySettings levelSettings = DifficultySettings.Current;

        // Fixed level length: the boss comes at exactly the same moment for everyone.
        bool due = isBoss && levelSettings.FixedLevelLengthActive
            ? RunTime >= levelSettings.levelLengthSeconds
            : dist <= triggerDistance || ShouldTriggerEarly();

        if (due)
        {
            SpawnEnemies();
            hasTriggered = true;
            clearedSince = -1f;
        }
    }

    /// <summary>
    /// True when every enemy is dead (for a short breather) and this is the
    /// closest wave that hasn't spawned yet, so the player isn't left waiting.
    /// </summary>
    private bool ShouldTriggerEarly()
    {
        DifficultySettings settings = DifficultySettings.Current;

        // Only regular waves are pulled in early; the boss keeps its place so the level length never shrinks.
        if (!settings.NextWaveWhenClearedActive || isBoss)
            return false;

        if (EnemyHealth.Alive.Count > 0 || BossHealth.AnyAlive)
        {
            clearedSince = -1f;
            return false;
        }

        if (clearedSince < 0f)
            clearedSince = Time.time;

        if (Time.time - clearedSince < settings.clearBreather)
            return false;

        return NextPendingSpawner() == this;
    }

    private EnemySpawner NextPendingSpawner()
    {
        EnemySpawner next = null;
        float best = float.MaxValue;

        foreach (EnemySpawner spawner in spawners)
        {
            if (spawner.hasTriggered || spawner.playerTarget == null)
                continue;

            float d = Vector3.Distance(spawner.transform.position, spawner.playerTarget.position);

            if (d < best)
            {
                best = d;
                next = spawner;
            }
        }

        return next;
    }

    private void SpawnEnemies()
    {
        DifficultySettings settings = DifficultySettings.Current;
        float power = DifficultySettings.PlayerPower();

        int count = enemyCount;
        float radius = spawnRadius;

        if (!isBoss)
        {
            float countMultiplier = settings.EnemyCountMultiplier(power);
            count = Mathf.Clamp(Mathf.RoundToInt(enemyCount * countMultiplier), enemyCount, Mathf.Max(enemyCount, settings.maxEnemiesPerWave));
            radius = spawnRadius * Mathf.Sqrt((float)count / Mathf.Max(1, enemyCount));
        }

        // A wave pulled in early spawns at trigger distance, not further away.
        Vector3 center = transform.position;
        Vector3 toCenter = center - playerTarget.position;
        toCenter.y = 0f;

        if (toCenter.magnitude > triggerDistance)
        {
            Vector3 clamped = playerTarget.position + toCenter.normalized * triggerDistance;
            center = new Vector3(clamped.x, center.y, clamped.z);
        }

        // Start just above the top of the screen so the wave walks in rather than popping in under the HUD.
        if (settings.SpawnOffscreenActive && Camera.main != null)
        {
            Ray topEdge = Camera.main.ViewportPointToRay(new Vector3(0.5f, 1f, 0f));
            Plane ground = new Plane(Vector3.up, new Vector3(0f, center.y, 0f));

            if (ground.Raycast(topEdge, out float hit))
            {
                float minZ = topEdge.GetPoint(hit).z + radius + (isBoss ? 2.5f : 1f);

                if (center.z < minZ)
                    center.z = Mathf.Min(minZ, center.z + 10f);
            }
        }

        List<Vector3> positions =
            GenerateClusterPositions(count, radius);

        foreach (Vector3 offset in positions)
        {
            Vector3 spawnPos =
                center + offset;

            GameObject enemyGO = Instantiate(
                enemyPrefab,
                spawnPos,
                Quaternion.identity
            );

            EnemyAI ai =
                enemyGO.GetComponent<EnemyAI>();

            if (ai != null)
            {
                ai.target = playerTarget;
                ai.minSpacing = settings.SpacingActive ? Mathf.Max(minSpacing, settings.enemySpacing) : minSpacing;
            }

            ApplyHealthScaling(enemyGO, power);

            // The boss keeps to the middle of the road (the boss spawner's line).
            if (isBoss && settings.BossCenteredActive)
            {
                BossAI boss = enemyGO.GetComponentInChildren<BossAI>();
                if (boss != null)
                    boss.SetCenterLine(transform.position.x);
            }

            if (destroyAfterSeconds > 0f)
            {
                Destroy(
                    enemyGO,
                    destroyAfterSeconds
                );
            }
        }

        if (isBoss)
        {
            Invoke(nameof(StopPlatform), 1);
            BossIntro.Show();
        }
    }

    /// <summary>Scales enemy / boss HP to the player's power.</summary>
    public static void ApplyHealthScaling(GameObject enemyGO, float power)
    {
        DifficultySettings settings = DifficultySettings.Current;

        EnemyHealth health = enemyGO.GetComponent<EnemyHealth>();
        if (health != null)
            health.SetMaxHealth(health.maxHealth * settings.EnemyHealthMultiplier(power));

        BossHealth bossHealth = enemyGO.GetComponentInChildren<BossHealth>();
        if (bossHealth != null)
            bossHealth.maxHealth *= settings.BossHealthMultiplier(power);
    }

    /// <summary>Enemy prefab of the first regular (non-boss) spawner, used for boss minions.</summary>
    public static GameObject FindRegularEnemyPrefab()
    {
        foreach (EnemySpawner spawner in spawners)
        {
            if (!spawner.isBoss && spawner.enemyPrefab != null)
                return spawner.enemyPrefab;
        }

        return null;
    }

    public void StopPlatform()
    {
        if (DifficultySettings.Current.StopClearOfGatesActive)
        {
            StartCoroutine(StopWhenClearOfGates());
            return;
        }

        playerTarget.GetComponent<ArcherPlayerController>().ForceIdleAnimation();
        platformMover.moveSpeed = 0;
    }

    /// <summary>Keeps scrolling (up to a few seconds) until no gate strip is under the player, then stops.</summary>
    private System.Collections.IEnumerator StopWhenClearOfGates()
    {
        float giveUpAt = Time.time + 6f;

        while (Time.time < giveUpAt && IsPlayerOnGate())
            yield return null;

        playerTarget.GetComponent<ArcherPlayerController>().ForceIdleAnimation();
        platformMover.moveSpeed = 0;
    }

    private bool IsPlayerOnGate()
    {
        float playerZ = playerTarget.position.z;

        foreach (PowerZoneTrigger gate in FindObjectsByType<PowerZoneTrigger>(FindObjectsSortMode.None))
        {
            // The visible strip is the gate's parent with all its sprites.
            Transform strip = gate.transform.parent != null ? gate.transform.parent : gate.transform;

            foreach (Renderer r in strip.GetComponentsInChildren<Renderer>())
            {
                if (playerZ > r.bounds.min.z - 0.8f && playerZ < r.bounds.max.z + 0.8f)
                    return true;
            }
        }

        return false;
    }

    private List<Vector3> GenerateClusterPositions(int count, float spawnRadius)
    {
        List<Vector3> result =
            new List<Vector3>();

        int columns =
            Mathf.CeilToInt(Mathf.Sqrt(count));

        int rows =
            Mathf.CeilToInt(
                (float)count / columns
            );

        float diameter =
            spawnRadius * 2f;

        float stepX =
            diameter / columns;

        float stepZ =
            diameter / rows;

        float spacing =
            Mathf.Max(
                minSpacing,
                Mathf.Min(stepX, stepZ)
            );

        int spawned = 0;

        for (int r = 0;
             r < rows && spawned < count;
             r++)
        {
            for (int c = 0;
                 c < columns && spawned < count;
                 c++)
            {
                float x =
                    (c - columns / 2f) *
                    spacing +
                    Random.Range(-0.2f, 0.2f);

                float z =
                    (r - rows / 2f) *
                    spacing +
                    Random.Range(-0.2f, 0.2f);

                Vector3 offset =
                    new Vector3(x, 0f, z);

                if (offset.magnitude > spawnRadius)
                {
                    offset =
                        offset.normalized *
                        spawnRadius;
                }

                result.Add(offset);
                spawned++;
            }
        }

        return result;
    }

    private void OnDrawGizmos()
    {
        // Spawn radius
        Gizmos.color = Color.yellow;

        Gizmos.DrawWireSphere(
            transform.position,
            spawnRadius
        );

        // Trigger distance
        Gizmos.color = Color.red;

        Gizmos.DrawLine(
            transform.position,
            transform.position +
            Vector3.back * triggerDistance
        );
    }
}