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

    // Fade-out (DifficultySettings.arrowFade): the arrow shrinks and its trail thins instead of vanishing.
    private Vector3 baseScale;
    private TrailRenderer[] trails;
    private float[] trailWidths;
    private float fadeLeft = -1f;
    private float fadeTotal;

    /// <summary>Already used up (e.g. absorbed by a gate) and only fading out; it can't hit enemies.</summary>
    public bool IsSpent { get; private set; }

    private static float screenTopDistance = -1f;
    private static float screenTopCheckedAt = -100f;

    private void Awake()
    {
        baseScale = transform.localScale;
        trails = GetComponentsInChildren<TrailRenderer>(true);
        trailWidths = new float[trails.Length];

        for (int i = 0; i < trails.Length; i++)
            trailWidths[i] = trails[i].widthMultiplier;
    }

    public void SetPool(ArcherArrowController arrowPool)
    {
        pool = arrowPool;
    }

    private void OnEnable()
    {
        collidedWithRB = false;
        lifeTimer = lifeTime;
        IsSpent = false;
        fadeLeft = -1f;
        ApplyFade(1f);

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

        if (fadeLeft >= 0f)
        {
            fadeLeft -= Time.deltaTime;
            ApplyFade(Mathf.Clamp01(fadeLeft / fadeTotal));

            if (fadeLeft <= 0f)
                ReturnToPool();
            return;
        }

        DifficultySettings settings = DifficultySettings.Current;

        // Damage range: past it the arrow can't hit anything and fades out
        if (settings.ArrowRangeActive && pool != null && OutOfRange(settings))
        {
            BeginFade(settings.arrowFadeTime, true);
            return;
        }

        // Automatic return by lifetime
        if (lifeTime > 0f)
        {
            lifeTimer -= Time.deltaTime;

            if (lifeTimer <= 0f)
            {
                EndFlight(settings);
                return;
            }
        }

        // Automatic return by distance (with fading on, arrows fly until past the top of the screen)
        if (pool != null && maxDistanceFromPool > 0f)
        {
            float limit = settings.ArrowFadeActive
                ? Mathf.Max(maxDistanceFromPool, ScreenTopDistance() + 3f)
                : maxDistanceFromPool;

            float distance = Vector3.Distance(
                transform.position,
                pool.transform.position
            );

            if (distance >= limit)
            {
                EndFlight(settings);
            }
        }
    }

    private bool OutOfRange(DifficultySettings settings)
    {
        Vector3 origin = pool.transform.position;

        if (transform.position.z - origin.z >= settings.arrowRange)
            return true;

        if (settings.arrowRangeStopsAtScreenEdge)
        {
            float edgeZ = DifficultySettings.ScreenTopGroundZ(origin.y, float.MaxValue);
            if (transform.position.z >= edgeZ - 0.5f)
                return true;
        }

        return false;
    }

    private void EndFlight(DifficultySettings settings)
    {
        if (settings.ArrowFadeActive)
            BeginFade(settings.arrowFadeTime, false);
        else
            ReturnToPool();
    }

    /// <summary>Keeps flying while shrinking away over 'duration', then returns to the pool.</summary>
    public void BeginFade(float duration, bool spent)
    {
        if (spent)
            IsSpent = true;

        if (fadeLeft >= 0f)
            return;

        fadeTotal = Mathf.Max(0.01f, duration);
        fadeLeft = fadeTotal;
    }

    private void ApplyFade(float amount)
    {
        transform.localScale = baseScale * amount;

        if (trails == null)
            return;

        for (int i = 0; i < trails.Length; i++)
        {
            if (trails[i] != null)
                trails[i].widthMultiplier = trailWidths[i] * amount;
        }
    }

    /// <summary>Ground distance from the player to the top edge of the screen (rechecked every couple of seconds).</summary>
    private static float ScreenTopDistance()
    {
        if (screenTopDistance < 0f || Time.time - screenTopCheckedAt > 2f)
        {
            screenTopCheckedAt = Time.time;
            Camera cam = Camera.main;

            if (cam != null)
            {
                Ray top = cam.ViewportPointToRay(new Vector3(0.5f, 1f, 0f));
                Plane ground = new Plane(Vector3.up, Vector3.zero);

                if (ground.Raycast(top, out float hit))
                    screenTopDistance = Mathf.Abs(top.GetPoint(hit).z - cam.transform.position.z) + 2f;
            }
        }

        return Mathf.Max(screenTopDistance, 0f);
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