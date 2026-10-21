using UnityEngine;

/// <summary>
/// Fills dead space: when the screen is nearly empty (before the boss), a small group of
/// enemies walks in from just above the top of the screen. Created by EnemySpawner.
/// </summary>
public class TrickleSpawner : MonoBehaviour
{
    public static TrickleSpawner Instance { get; private set; }

    private float nextSpawn = -1f;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        DifficultySettings settings = DifficultySettings.Current;

        if (!settings.TrickleActive || !ArcherGameState.IsGameRunning)
        {
            nextSpawn = -1f;
            return;
        }

        // Short grace period at the start of a run.
        if (nextSpawn < 0f)
            nextSpawn = Time.time + 3f;

        if (EnemySpawner.BossTriggered || Time.time < nextSpawn)
            return;

        if (EnemyHealth.Alive.Count > settings.trickleWhenAliveAtMost)
            return;

        nextSpawn = Time.time + settings.trickleInterval;

        Transform player = EnemySpawner.FindPlayerTarget();
        GameObject prefab = EnemySpawner.FindRegularEnemyPrefab();

        if (player == null || prefab == null)
            return;

        float power = DifficultySettings.PlayerPower();
        int count = Mathf.Max(1, Mathf.RoundToInt(Random.Range(settings.trickleGroupMin, settings.trickleGroupMax + 1) * settings.EnemyCountMultiplier(power)));

        float y = EnemySpawner.GroundY;
        float z = DifficultySettings.ScreenTopGroundZ(y, player.position.z + 16f) + 1.5f;
        float centerX = Random.Range(-2f, 2f);

        for (int i = 0; i < count; i++)
        {
            Vector3 position = new Vector3(centerX + Random.Range(-1.6f, 1.6f), y, z + Random.Range(0f, 2.5f));
            EnemySpawner.SpawnEnemy(prefab, position, player, power);
        }
    }
}
