using System.Collections.Generic;
using UnityEngine;

public class ArrowController : MonoBehaviour
{
    public List<ArrowProperties> arrowProperties = new();
    public bool collidedWithRB;
    public ArrowType arrowType = ArrowType.SimpleArrow;
    public float damage;

    [Header("Movement")]
    public float moveSpeed = 20f;

    [Header("Pool")]
    public float lifeTime = 5f;

    [Header("Distance Limit")]
    public float maxDistanceFromPool = 30f;

    private ArcherArrowController pool;
    private float lifeTimer;

    public void SetPool(ArcherArrowController arrowPool)
    {
        pool = arrowPool;
    }

    private void OnEnable()
    {
        collidedWithRB = false;
        lifeTimer = lifeTime;

        if (ArcherController.Instance)
        {
            ArcherController.Instance.OnArrowChange += OnArrowChange;
            OnArrowChange(ArcherController.Instance.currentArrowType);
        }
    }

    private void OnDisable()
    {
        if (ArcherController.Instance)
        {
            ArcherController.Instance.OnArrowChange -= OnArrowChange;
        }
    }

    private void OnArrowChange(ArrowType arrow)
    {
        for (int i = 0; i < arrowProperties.Count; i++)
        {
            ArrowProperties property = arrowProperties[i];

            bool isSelected = property.arrowType == arrow;

            // Enable/disable arrow visual
            if (property.arrowObjects != null)
            {
                for (int j = 0; j < property.arrowObjects.Length; j++)
                {
                    if (property.arrowObjects[j] != null)
                    {
                        property.arrowObjects[j].SetActive(isSelected);
                    }
                }
            }

            // Enable/disable trail
            if (property.trailRenderer != null)
            {
                property.trailRenderer.SetActive(isSelected);
            }
        }

        // Update the actual arrow type
        arrowType = arrow;
    }

    private void Update()
    {
        // Move forward
        transform.position += transform.forward * moveSpeed * Time.deltaTime;

        // Automatic return by lifetime
        if (lifeTime > 0f)
        {
            lifeTimer -= Time.deltaTime;

            if (lifeTimer <= 0f)
            {
                ReturnToPool();
                return;
            }
        }

        // Automatic return by distance
        if (pool != null && maxDistanceFromPool > 0f)
        {
            float distance = Vector3.Distance(
                transform.position,
                pool.transform.position
            );

            if (distance >= maxDistanceFromPool)
            {
                ReturnToPool();
            }
        }
    }

    public void ReverseDirection(Vector3 wallNormal)
    {
        if (arrowType != ArrowType.ReversedArrow)
            return;

        Vector3 reflectedDirection = Vector3.Reflect(
            transform.forward,
            wallNormal.normalized
        );

        transform.rotation = Quaternion.LookRotation(
            reflectedDirection,
            transform.up
        );
    }

    public void ReturnToPool()
    {
        if (!gameObject.activeSelf)
            return;

        collidedWithRB = false;

        // Disable the currently active trail
        for (int i = 0; i < arrowProperties.Count; i++)
        {
            if (arrowProperties[i].trailRenderer != null)
            {
                arrowProperties[i].trailRenderer.SetActive(false);
            }
        }

        if (pool != null)
        {
            pool.ReturnArrow(gameObject);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;

        Vector3 center = pool != null
            ? pool.transform.position
            : transform.position;

        Gizmos.DrawWireSphere(center, maxDistanceFromPool);
    }
}

[System.Serializable]
public enum ArrowType
{
    SimpleArrow,
    ReversedArrow,
    FreezeArrow,
    FireArrow,
    ExpandAngleArrow
}

[System.Serializable]
public struct ArrowProperties
{
    public ArrowType arrowType;
    public GameObject[] arrowObjects;
    public GameObject trailRenderer;
}