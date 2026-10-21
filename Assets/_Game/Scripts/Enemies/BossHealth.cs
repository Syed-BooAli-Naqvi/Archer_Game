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

    private static readonly System.Collections.Generic.HashSet<BossHealth> alive =
        new System.Collections.Generic.HashSet<BossHealth>();

    public static bool AnyAlive => alive.Count > 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        alive.Clear();
    }

    private void OnEnable()
    {
        if (!isDead)
            alive.Add(this);
    }

    private void OnDisable()
    {
        alive.Remove(this);
    }

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

        DamageNumbers.Show(transform.position + Vector3.up * 3.5f, damage, DamageNumbers.Style.Boss, 1.4f, GetInstanceID());

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
        alive.Remove(this);

        // Normal death
        Die();
    }

    public void Die()
    {
        // The player died first: the run is already lost.
        if (RunEnding.Ended)
            return;

        MainMenuUIHandler.Instance.AddToReward(rewardAmount);

        SoundManager.Instance.PlaySound(SoundName.Win);
        Haptics.Play(HapticType.Success);

        RunEnding.Win((bossObject != null ? bossObject.transform : transform).position);

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

        if (arrow == null || arrow.IsSpent)
            return;

        arrow.ReturnToPool();

        TakeDamage(arrow.damage);
    }
}