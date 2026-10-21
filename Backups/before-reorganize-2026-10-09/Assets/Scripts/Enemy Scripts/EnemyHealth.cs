using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;

public class EnemyHealth : MonoBehaviour
{
    public int rewardAmount;
    public EnemyAI enemyAI;

    [Header("Health Settings")]
    public float maxHealth = 100f;
    public float currentHealth;

    [Header("Health UI")]
    public Slider healthSlider;

    [Header("Death")]
    public float destroyDelay = 1.5f;
    public GameObject deathEffectPrefab;
    public GameObject hitEffectPrefab;

    [Header("Death Animators")]
    public List<Animator> animators;
    public string dyingAnimation = "Dying";

    public event Action<EnemyHealth> OnDeath;
    public event Action<float, float> OnHealthChanged;

    private bool isDead = false;

    // Living (not yet dead) enemies, used to detect when a wave has been cleared.
    public static readonly HashSet<EnemyHealth> Alive = new HashSet<EnemyHealth>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Alive.Clear();
    }

    private void OnEnable()
    {
        if (!isDead)
            Alive.Add(this);
    }

    private void OnDisable()
    {
        Alive.Remove(this);
    }

    // World-space health bar keeps its starting rotation instead of tilting as the enemy turns.
    private Transform healthBarRoot;
    private Quaternion healthBarRotation;

    private void Start()
    {
        if (healthSlider == null)
            return;

        Canvas canvas = healthSlider.GetComponentInParent<Canvas>();

        if (canvas != null && canvas.renderMode == RenderMode.WorldSpace && canvas.transform != transform)
        {
            healthBarRoot = canvas.transform;
            healthBarRotation = healthBarRoot.rotation;
        }
    }

    private void LateUpdate()
    {
        if (healthBarRoot != null)
            healthBarRoot.rotation = healthBarRotation;
    }

    public void SetMaxHealth(float value)
    {
        maxHealth = value;
        currentHealth = value;

        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHealth;
            healthSlider.value = currentHealth;
        }
    }

    private void Awake()
    {
        currentHealth = maxHealth;

        if (healthSlider != null)
        {
            healthSlider.minValue = 0f;
            healthSlider.maxValue = maxHealth;
            healthSlider.value = currentHealth;
        }

        if (enemyAI == null)
            enemyAI = GetComponent<EnemyAI>();
    }

    public void TakeDamage(float amount)
    {
        if (!ArcherGameState.IsGameRunning)
            return;

        if (isDead)
            return;

        HitEffect();

        if (healthSlider != null)
            healthSlider.gameObject.SetActive(true);

        currentHealth -= amount;
        currentHealth = Mathf.Max(currentHealth, 0f);

        if (healthSlider != null)
            healthSlider.value = currentHealth;

        DamageNumbers.Show(transform.position + Vector3.up * 2f, amount, NumberStyle(), 1f, GetInstanceID());

        OnHealthChanged?.Invoke(
            currentHealth,
            maxHealth
        );

        if (currentHealth <= 0f)
        {
            if (enemyAI != null &&
                enemyAI.TryPlayStatusDeath())
            {
                return;
            }

            Die();
        }
    }

    public void Die()
    {
        if (isDead)
            return;

        isDead = true;
        Alive.Remove(this);

        // Punchy kill sound (throttled so big multi-kills don't stack into noise).
        if (DifficultySettings.Current.KillSoundActive && SoundManager.Instance != null && Time.unscaledTime - lastKillSoundTime > 0.06f)
        {
            lastKillSoundTime = Time.unscaledTime;
            SoundManager.Instance.PlaySound(SoundName.Kill);
        }

        if (healthSlider != null)
            healthSlider.gameObject.SetActive(false);

        MainMenuUIHandler.Instance.AddToReward(rewardAmount);

        OnDeath?.Invoke(this);

        // Play Dying animation on all animators
        foreach (Animator animator in animators)
        {
            if (animator != null && animator.isActiveAndEnabled)
            {
                animator.SetLayerWeight(1, 0);
                animator.Play(dyingAnimation);
            }
        }

        // Disable AI so the body stops walking and attacking while the dying animation plays.
        if (enemyAI != null)
            enemyAI.enabled = false;

        // Disable collision
        Collider col = GetComponent<Collider>();

        if (col != null)
            col.enabled = false;

        // Death effect
        if ((enemyAI == null || enemyAI.activeEffect == EnemyAI.StatusEffect.None) && deathEffectPrefab != null)
        {
            Instantiate(
                deathEffectPrefab,
                new Vector3(transform.position.x, transform.position.y + 2, transform.position.z),
                Quaternion.identity
            );
        }

        Destroy(
            gameObject,
            destroyDelay
        );
    }

    private static float lastKillSoundTime = -1f;

    private DamageNumbers.Style NumberStyle()
    {
        if (enemyAI != null && enemyAI.activeEffect == EnemyAI.StatusEffect.Fire)
            return DamageNumbers.Style.Fire;

        if (enemyAI != null && enemyAI.activeEffect == EnemyAI.StatusEffect.Freeze)
            return DamageNumbers.Style.Frost;

        return DamageNumbers.Style.Normal;
    }

    public void HitEffect()
    {
        SoundManager.Instance.PlaySoundUnique(SoundName.HitEffect);
        if (hitEffectPrefab != null)
        {
            GameObject effect = Instantiate(
                hitEffectPrefab,
                new Vector3(transform.position.x, transform.position.y, transform.position.z),
                Quaternion.identity
            );

            DifficultySettings.ApplyParticleBudget(effect);

            // The hit effect prefab doesn't destroy itself, so clean it up once it has finished.
            Destroy(effect, EffectLifetime(effect));
        }
    }

    private static float EffectLifetime(GameObject effect)
    {
        float longest = 0.5f;

        foreach (ParticleSystem ps in effect.GetComponentsInChildren<ParticleSystem>())
        {
            ParticleSystem.MainModule main = ps.main;
            longest = Mathf.Max(longest, main.startDelay.constantMax + main.duration + main.startLifetime.constantMax);
        }

        return longest + 0.2f;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!ArcherGameState.IsGameRunning)
            return;

        ArrowController arrow =
            other.GetComponent<ArrowController>();

        if (arrow == null || arrow.IsSpent)
            return;

        if (enemyAI != null &&
            !enemyAI.IsFrozen())
        {
            arrow.ReturnToPool();
        }

        switch (arrow.arrowType)
        {
            case ArrowType.SimpleArrow:
            case ArrowType.ReversedArrow:
                TakeDamage(arrow.damage);
                break;

            case ArrowType.FreezeArrow:
                if (enemyAI != null)
                    enemyAI.FreezeEnemy(arrow.damage);

                TakeDamage(arrow.damage);
                break;

            case ArrowType.FireArrow:
                if (enemyAI != null)
                    enemyAI.FireEnemy(arrow.damage);

                TakeDamage(arrow.damage);
                break;
        }
    }
}