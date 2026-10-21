using UnityEngine;
using UnityEngine.UI;

public class ArcherPlayerHealth : MonoBehaviour
{
    [Header("Health Settings")]
    public float maxHealth = 100f;
    public float currentHealth;

    [Header("Health UI")]
    public Slider healthSlider;

    [Header("Death")]
    public float destroyDelay = 1.5f;
    public GameObject deathEffectPrefab;

    [Header("Animator")]
    public Animator animator;
    public string dyingAnimation = "Dying";

    private bool isDead = false;

    private void Awake()
    {
        currentHealth = maxHealth;

        if (healthSlider != null)
        {
            healthSlider.minValue = 0f;
            healthSlider.maxValue = maxHealth;
            healthSlider.value = currentHealth;
        }

        if (animator == null)
            animator = GetComponent<Animator>();

        StyleHealthBar();

        if (DifficultySettings.Current.HitFeedbackActive)
            HealthBarChip.Attach(healthSlider);
    }

    // Thicker dark frame and an outline so the bar reads like the enemy bars even at full health.
    private void StyleHealthBar()
    {
        if (healthSlider == null || !DifficultySettings.Current.enabled)
            return;

        if (healthSlider.fillRect != null)
            healthSlider.fillRect.sizeDelta = new Vector2(-14f, -14f);

        Image frame = healthSlider.GetComponent<Image>();

        if (frame != null && frame.GetComponent<Outline>() == null)
        {
            Outline outline = frame.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            outline.effectDistance = new Vector2(4f, -4f);
        }
    }

    public void TakeDamage(float amount)
    {
        // Once the boss is beaten the run is won; nothing can hurt the player any more.
        if (isDead || RunEnding.Won) return;

        currentHealth -= amount;
        currentHealth = Mathf.Max(currentHealth, 0f);

        UpdateHealthSlider();

        if (currentHealth <= 0f)
            Die();
        else
        {
            if (DifficultySettings.Current.HitFeedbackActive)
                HitFlash.Flash(amount / maxHealth);

            if (Time.unscaledTime - lastHurtHaptic >= 0.3f)
            {
                // Light tap and hurt sound per hit (throttled, so a crowd of enemies isn't one long buzz).
                lastHurtHaptic = Time.unscaledTime;
                Haptics.Play(HapticType.Medium);
                SoundManager.Instance.PlaySound(SoundName.PlayerHurt);
            }
        }
    }

    private void UpdateHealthSlider()
    {
        if (healthSlider != null)
            healthSlider.value = currentHealth;
    }

    private float lastHurtHaptic = -10f;

    private void Die()
    {
        if (isDead) return;

        isDead = true;

        Haptics.Play(HapticType.Failure);

        if (DifficultySettings.Current.HitFeedbackActive)
            HitFlash.Flash(1f);

        RunEnding.Lose();

        SoundManager.Instance.PlaySound(SoundName.Die);
        
        // Play dying animation
        if (animator != null)
        {
            animator.SetLayerWeight(1, 0);
            animator.Play(dyingAnimation);
        }

        // Disable AI
        var ai = GetComponent<EnemyAI>();
        if (ai != null)
            ai.enabled = false;

        // Disable collision
        var col = GetComponent<Collider>();
        if (col != null)
            col.enabled = false;

        // Death effect
        if (deathEffectPrefab != null)
        {
            Instantiate(
                deathEffectPrefab,
                transform.position,
                Quaternion.identity
            );
        }

        Destroy(gameObject, destroyDelay);
    }
}