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

        if (healthSlider != null)
            healthSlider.gameObject.SetActive(false);

        MainMenuUIHandler.Instance.AddToReward(rewardAmount);

        OnDeath?.Invoke(this);

        // Play Dying animation on all animators
        foreach (Animator animator in animators)
        {
            if (animator != null)
            {
                animator.SetLayerWeight(1, 0);
                animator.Play(dyingAnimation);
            }
        }

        // Disable AI
        // EnemyAI ai = GetComponent<EnemyAI>();

        // if (ai != null)
        //     ai.enabled = false;

        // Disable collision
        Collider col = GetComponent<Collider>();

        if (col != null)
            col.enabled = false;

        // Death effect
        if (enemyAI.activeEffect == EnemyAI.StatusEffect.None && deathEffectPrefab != null)
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

    public void HitEffect()
    {
        SoundManager.Instance.PlaySoundUnique(SoundName.HitEffect);
        // Death effect
        if (hitEffectPrefab != null)
        {
            Instantiate(
                hitEffectPrefab,
                new Vector3(transform.position.x, transform.position.y, transform.position.z),
                Quaternion.identity
            );
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!ArcherGameState.IsGameRunning)
            return;

        ArrowController arrow =
            other.GetComponent<ArrowController>();

        if (arrow == null)
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