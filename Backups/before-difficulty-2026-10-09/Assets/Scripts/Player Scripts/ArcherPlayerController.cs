using UnityEngine;

public class ArcherPlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;

    [Header("Animator")]
    public Animator animator;

    [SerializeField] private string idleAnimation = "Idle";
    [SerializeField] private string runningAnimation = "Running";
    [SerializeField] private int bowAttackLayer = 1;

    bool stopPlayer;

    private void Update()
    {
        if (stopPlayer)
        {
            return;
        }
        bool isRunning = ArcherGameState.IsGameRunning;

        // Update animations
        UpdateAnimation(isRunning);

        // Game has not started
        if (!isRunning)
            return;

        // Move forward
        transform.position +=
            Vector3.forward *
            moveSpeed *
            Time.deltaTime;
    }

    private void UpdateAnimation(bool isRunning)
    {
        if (animator == null)
            return;

        if (isRunning)
        {
            animator.Play(runningAnimation, 0);
            animator.SetLayerWeight(bowAttackLayer, 1f);
        }
        else
        {
            animator.Play(idleAnimation, 0);
            animator.SetLayerWeight(bowAttackLayer, 0f);
        }
    }

    public void ForceIdleAnimation()
    {
        stopPlayer = true;
        if (animator == null)
            return;

        animator.Play(idleAnimation, 0);
        animator.SetLayerWeight(bowAttackLayer, 0f);
    }
}