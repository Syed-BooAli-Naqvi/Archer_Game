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
        if (isDead) return;

        currentHealth -= amount;
        currentHealth = Mathf.Max(currentHealth, 0f);

        UpdateHealthSlider();

        if (currentHealth <= 0f)
            Die();
    }

    private void UpdateHealthSlider()
    {
        if (healthSlider != null)
            healthSlider.value = currentHealth;
    }

    private void Die()
    {
        if (isDead) return;

        isDead = true;

        MainMenuUIHandler.Instance.OpenFailedPanel();

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