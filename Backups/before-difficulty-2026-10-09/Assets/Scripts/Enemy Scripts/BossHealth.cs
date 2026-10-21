using UnityEngine;
using UnityEngine.UI;

public class BossHealth : MonoBehaviour
{
    public int rewardAmount;
    [Header("Health")]
    public float maxHealth = 500f;
    public float currentHealth;

    [Header("UI")]
    public Slider healthSlider;

    [Header("Boss AI")]
    public BossAI bossAI;

    [Header("Death")]
    public GameObject bossObject;

    private bool isDead = false;

    private void Start()
    {
        currentHealth = maxHealth;

        if (bossAI == null)
            bossAI = GetComponent<BossAI>();

        UpdateHealthUI();
    }

    public void TakeDamage(float damage)
    {
        if (isDead)
            return;

        if (damage <= 0f)
            return;

        currentHealth -= damage;

        if (currentHealth < 0f)
            currentHealth = 0f;

        UpdateHealthUI();

        if (currentHealth <= 0f)
        {
            HandleDeath();
        }
    }

    private void HandleDeath()
    {
        if (isDead)
            return;

        isDead = true;

        // Normal death
        Die();
    }

    public void Die()
    {
        MainMenuUIHandler.Instance.AddToReward(rewardAmount);

        SoundManager.Instance.PlaySound(SoundName.Win);
        
        MainMenuUIHandler.Instance.OpenCompletePanel();
        
        if (bossObject != null)
        {
            bossObject.SetActive(false);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private void UpdateHealthUI()
    {
        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHealth;
            healthSlider.value = currentHealth;
        }
    }

    public float GetHealthPercent()
    {
        if (maxHealth <= 0f)
            return 0f;

        return currentHealth / maxHealth;
    }

    public bool IsDead()
    {
        return isDead;
    }

    private void OnTriggerEnter(Collider other)
    {
        // Don't process arrows while paused
        if (!ArcherGameState.IsGameRunning)
            return;

        ArrowController arrow =
            other.GetComponent<ArrowController>();

        if (arrow == null)
            return;

        arrow.ReturnToPool();

        TakeDamage(arrow.damage);
    }
}