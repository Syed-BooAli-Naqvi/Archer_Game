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
    float lastX;

    // The model's resting offset under the Player object. Running/Idle clips carry root motion,
    // which can drag the model away from the Player (and its health bar); keep it pinned.
    Transform model;
    Vector3 modelLocalPosition;
    Quaternion modelLocalRotation;

    private void Awake()
    {
        if (animator != null && animator.transform != transform)
        {
            model = animator.transform;
            modelLocalPosition = model.localPosition;
            modelLocalRotation = model.localRotation;
        }
    }

    private void LateUpdate()
    {
        if (model == null || !DifficultySettings.Current.enabled)
            return;

        model.localPosition = modelLocalPosition;
        model.localRotation = modelLocalRotation;
    }

    private void Update()
    {
        if (stopPlayer)
        {
            UpdateStoppedAnimation();
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
        lastX = transform.position.x;
        if (animator == null)
            return;

        animator.Play(idleAnimation, 0);
        animator.SetLayerWeight(bowAttackLayer, 0f);
    }

    // After the platform stops (boss fight) the player can still steer and keeps shooting:
    // run while moving sideways, stand idle otherwise, and keep the bow animation on.
    private void UpdateStoppedAnimation()
    {
        float sideSpeed = Mathf.Abs(transform.position.x - lastX) / Mathf.Max(Time.deltaTime, 0.0001f);
        lastX = transform.position.x;

        // Time stops on the win/fail panel; don't override the dying animation.
        if (animator == null || !DifficultySettings.Current.enabled || Time.timeScale == 0f)
            return;

        bool shooting = ArcherGameState.IsGameRunning;
        string wanted = sideSpeed > 0.5f ? runningAnimation : idleAnimation;

        if (!animator.GetCurrentAnimatorStateInfo(0).IsName(wanted) && !animator.IsInTransition(0))
            animator.CrossFadeInFixedTime(wanted, 0.1f, 0);

        animator.SetLayerWeight(bowAttackLayer, shooting ? 1f : 0f);
    }
}