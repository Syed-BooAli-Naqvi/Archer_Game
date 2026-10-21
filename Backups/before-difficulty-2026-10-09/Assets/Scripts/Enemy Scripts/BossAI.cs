using System.Collections.Generic;
using UnityEngine;

public class BossAI : MonoBehaviour
{
    private enum State
    {
        Approach,
        Attack,
        Idle
    }

    public BossHealth myHealth;

    [Header("Target")]
    public Transform target;
    public string targetTag = "Player";

    [Header("Movement")]
    public float moveSpeed = 2f;
    public float stoppingDistance = 2f;

    [Header("Attack")]
    public float attackRange = 2.5f;
    public float attackDamage = 25f;
    public float attackCooldown = 1.5f;

    [Header("Animators")]
    public List<Animator> animators = new List<Animator>();
    public string attackTrigger = "Sword Attack";

    private State state = State.Approach;

    private float lastAttackTime = -999f;

    private ArcherPlayerHealth targetHealth;

    private bool isDying = false;

    private void Start()
    {
        SetupAnimators();

        if (target == null)
        {
            GameObject found =
                GameObject.FindGameObjectWithTag(targetTag);

            if (found != null)
                target = found.transform;
        }

        if (target != null)
            targetHealth =
                target.GetComponent<ArcherPlayerHealth>();
    }

    private void SetupAnimators()
    {
        if (animators == null)
            animators = new List<Animator>();

        if (animators.Count == 0)
        {
            Animator[] foundAnimators =
                GetComponentsInChildren<Animator>(true);

            animators.AddRange(foundAnimators);
        }

        animators.RemoveAll(animator => animator == null);
    }

    private void Update()
    {
        if (!ArcherGameState.IsGameRunning)
            return;

        if (isDying)
            return;

        if (target == null)
            return;

        float dist = Vector3.Distance(
            transform.position,
            target.position
        );

        switch (state)
        {
            case State.Approach:

                // Stop approaching once stopping distance is reached.
                if (dist <= stoppingDistance)
                {
                    state = State.Attack;
                    break;
                }

                MoveTowardTarget();
                break;

            case State.Attack:

                // If pushed too far away, approach again.
                if (dist > stoppingDistance + 0.25f)
                {
                    state = State.Approach;
                    break;
                }

                FaceTarget();
                TryAttack();
                break;

            case State.Idle:
                break;
        }
    }

    private float CurrentSpeed()
    {
        return moveSpeed;
    }

    private void MoveTowardTarget()
    {
        Vector3 dir =
            target.position -
            transform.position;

        dir.y = 0f;

        float distance = dir.magnitude;

        // Don't move closer than stopping distance.
        if (distance <= stoppingDistance)
        {
            state = State.Attack;
            return;
        }

        if (dir.sqrMagnitude <= 0.001f)
            return;

        dir.Normalize();

        float moveAmount =
            CurrentSpeed() *
            Time.deltaTime;

        // Prevent overshooting the stopping distance.
        float allowedDistance =
            distance - stoppingDistance;

        moveAmount =
            Mathf.Min(moveAmount, allowedDistance);

        transform.position +=
            dir * moveAmount;

        transform.rotation =
            Quaternion.LookRotation(dir);
    }

    private void FaceTarget()
    {
        Vector3 dir =
            target.position -
            transform.position;

        dir.y = 0f;

        if (dir.sqrMagnitude > 0.001f)
        {
            transform.rotation =
                Quaternion.LookRotation(dir);
        }
    }

    private void TryAttack()
    {
        if (Time.time - lastAttackTime < attackCooldown)
            return;

        lastAttackTime = Time.time;

        PlayAttackAnimation();

        if (targetHealth != null)
            targetHealth.TakeDamage(attackDamage);
    }

    private void PlayAttackAnimation()
    {
        if (animators == null)
            return;

        for (int i = 0; i < animators.Count; i++)
        {
            if (animators[i] == null)
                continue;

            animators[i].SetTrigger(attackTrigger);
        }
    }

    // =========================================================
    // DEATH
    // =========================================================

    public void Die()
    {
        if (myHealth != null)
            myHealth.Die();
    }
}