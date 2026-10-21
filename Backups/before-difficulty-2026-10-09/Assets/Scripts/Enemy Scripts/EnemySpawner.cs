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

        if (dist <= triggerDistance)
        {
            SpawnEnemies();
            hasTriggered = true;
        }
    }

    private void SpawnEnemies()
    {
        List<Vector3> positions =
            GenerateClusterPositions(enemyCount);

        foreach (Vector3 offset in positions)
        {
            Vector3 spawnPos =
                transform.position + offset;

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
                ai.minSpacing = minSpacing;
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
        }
    }

    public void StopPlatform()
    {
        playerTarget.GetComponent<ArcherPlayerController>().ForceIdleAnimation();
        platformMover.moveSpeed = 0;
    }

    private List<Vector3> GenerateClusterPositions(int count)
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