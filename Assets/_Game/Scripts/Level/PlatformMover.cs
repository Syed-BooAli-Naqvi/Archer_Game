using UnityEngine;

public class PlatformMover : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public Vector3 direction;

    private void Update()
    {
        bool isRunning = ArcherGameState.IsGameRunning;
        
        // Game has not started
        if (!isRunning)
            return;

        // Move forward
        transform.position +=
            direction *
            moveSpeed *
            Time.deltaTime;
    }
}