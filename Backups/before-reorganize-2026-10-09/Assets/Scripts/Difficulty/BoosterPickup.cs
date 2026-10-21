using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Booster: every so often (before the boss) a glowing booster target appears on the track.
/// It has health - a number of arrows must hit it. When it breaks, the booster is loaded and a
/// "2X" button appears; tapping it fires extra volleys of (current arrow count x 2), twice.
/// Created by EnemySpawner.
/// </summary>
public class BoosterPickup : MonoBehaviour
{
    public static BoosterPickup Instance { get; private set; }

    /// <summary>Playtest stats for this session.</summary>
    public static int Loaded, Fired;

    private static Material glowMaterial;
    private static RectTransform buttonRect;

    private readonly List<BoosterTarget> targets = new List<BoosterTarget>();
    private float nextSpawn = -1f;
    private int charges;

    private GameObject buttonRoot;
    private TextMeshProUGUI chargeBadge;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        if (buttonRoot != null)
            Destroy(buttonRoot);
    }

    /// <summary>True when the screen point is on the booster button (so touches there don't steer the archer).</summary>
    public static bool IsOverButton(Vector2 screenPoint)
    {
        return buttonRect != null && buttonRect.gameObject.activeInHierarchy &&
               RectTransformUtility.RectangleContainsScreenPoint(buttonRect, screenPoint, null);
    }

    private void Update()
    {
        DifficultySettings settings = DifficultySettings.Current;
        Transform player = EnemySpawner.FindPlayerTarget();

        if (!settings.BoosterActive || player == null)
            return;

        if (ArcherGameState.IsGameRunning && !EnemySpawner.BossTriggered)
        {
            if (nextSpawn < 0f)
                nextSpawn = Time.time + settings.boosterInterval * 0.5f;

            if (Time.time >= nextSpawn)
            {
                nextSpawn = Time.time + settings.boosterInterval;
                Spawn(settings, player);
            }
        }

        // Remove targets that passed the player, or when the boss arrives (the ground stops).
        for (int i = targets.Count - 1; i >= 0; i--)
        {
            BoosterTarget target = targets[i];

            if (target == null)
            {
                targets.RemoveAt(i);
                continue;
            }

            if (EnemySpawner.BossTriggered || target.transform.position.z < player.position.z - 3f)
            {
                Destroy(target.gameObject);
                targets.RemoveAt(i);
            }
        }

        UpdateButton(settings);
    }

    // ---------------------------------------------------------------- target on the track

    private void Spawn(DifficultySettings settings, Transform player)
    {
        PlatformMover platform = EnemySpawner.FindPlatformMover();
        float ground = EnemySpawner.GroundY;
        float z = DifficultySettings.ScreenTopGroundZ(ground, player.position.z + 16f) + 1.5f;

        GameObject root = new GameObject("Booster Target");
        root.transform.position = new Vector3(Random.Range(-2.2f, 2.2f), ground, z);

        if (platform != null)
            root.transform.SetParent(platform.transform, true);

        // Arrows are triggers without rigidbodies, so the target needs a kinematic body to receive hits.
        Rigidbody body = root.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        SphereCollider hitArea = root.AddComponent<SphereCollider>();
        hitArea.isTrigger = true;
        hitArea.center = new Vector3(0f, 0.7f, 0f);
        hitArea.radius = 0.8f;

        // Visual: a bucket full of the game's own arrows (falls back to a glowing orb if the model is missing).
        Transform visual = BuildArrowBucket();
        if (visual == null)
            visual = BuildFallbackOrb();
        visual.SetParent(root.transform, false);

        BoosterTarget target = root.AddComponent<BoosterTarget>();
        target.Init(this, visual, Mathf.Max(1, settings.boosterHealth));
        targets.Add(target);
    }

    private static Transform BuildArrowBucket()
    {
        Transform bucket = ModelUtil.InstantiateWithHeight("Kenney/Survival/barrel-open", 0.95f, "Arrow Bucket");
        if (bucket == null)
            return null;

        // Copy the fire-arrow visual from the arrow prefab (bright, easy to see) and stand a bunch up in the barrel.
        ArcherArrowController shooter = FindAnyObjectByType<ArcherArrowController>();
        Transform arrowMesh = null;
        if (shooter != null && shooter.arrowPrefab != null)
        {
            ArrowController arrowCtrl = shooter.arrowPrefab.GetComponent<ArrowController>();
            if (arrowCtrl != null)
                foreach (ArrowProperties property in arrowCtrl.arrowProperties)
                    if (property.arrowType == ArrowType.FireArrow && property.arrowObjects != null && property.arrowObjects.Length > 0 && property.arrowObjects[0] != null)
                        arrowMesh = property.arrowObjects[0].transform;

            if (arrowMesh == null)
                foreach (Transform child in shooter.arrowPrefab.GetComponentsInChildren<Transform>(true))
                    if (child.name == "Arrow Mesh") { arrowMesh = child; break; }
        }

        if (arrowMesh != null)
        {
            const int count = 7;
            for (int i = 0; i < count; i++)
            {
                // Pivot whose "forward" points up, so arrows (which fly along forward) stand upright.
                Transform pivot = new GameObject("Arrow").transform;
                pivot.SetParent(bucket, false);

                float angle = i * Mathf.PI * 2f / count;
                float radius = i == 0 ? 0f : 0.2f;
                pivot.localPosition = new Vector3(Mathf.Cos(angle) * radius, 1.05f, Mathf.Sin(angle) * radius);
                // Fan outward a little so each arrow is visible from the top-down camera.
                pivot.localRotation = Quaternion.LookRotation(new Vector3(Mathf.Cos(angle) * (i == 0 ? 0f : 0.35f), 1f, Mathf.Sin(angle) * (i == 0 ? 0f : 0.35f)));

                GameObject arrow = Instantiate(arrowMesh.gameObject, pivot);
                arrow.SetActive(true);
                arrow.transform.localPosition = arrowMesh.localPosition;
                arrow.transform.localRotation = arrowMesh.localRotation;
                arrow.transform.localScale = arrowMesh.localScale;
                foreach (Renderer r in arrow.GetComponentsInChildren<Renderer>())
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                // Size each arrow to about 0.9 m long.
                Renderer[] renderers = arrow.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    Bounds b = renderers[0].bounds;
                    foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
                    float length = Mathf.Max(b.size.x, b.size.y, b.size.z);
                    pivot.localScale = Vector3.one * (1.25f / Mathf.Max(0.0001f, length / Mathf.Max(0.0001f, bucket.lossyScale.y)));
                }
            }
        }

        return bucket;
    }

    private static Transform BuildFallbackOrb()
    {
        if (glowMaterial == null)
            glowMaterial = Resources.Load<Material>("BoosterGlow");

        GameObject orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(orb.GetComponent<Collider>());
        orb.transform.localPosition = new Vector3(0f, 0.7f, 0f);
        orb.transform.localScale = Vector3.one * 0.9f;
        if (glowMaterial != null)
            orb.GetComponent<Renderer>().sharedMaterial = glowMaterial;
        return orb.transform;
    }

    public static TextMeshPro CreateWorldLabel(Transform parent, string text, float size)
    {
        GameObject go = new GameObject("Label");
        go.transform.SetParent(parent, false);

        TextMeshPro label = go.AddComponent<TextMeshPro>();
        if (DifficultySettings.Current.damageNumberFont != null)
            label.font = DifficultySettings.Current.damageNumberFont;

        label.text = text;
        label.fontSize = size;
        label.alignment = TextAlignmentOptions.Center;
        label.rectTransform.sizeDelta = new Vector2(6f, 1.5f);
        label.enableVertexGradient = true;
        label.colorGradient = new VertexGradient(new Color(0.9f, 1f, 1f), new Color(0.9f, 1f, 1f), new Color(0.2f, 0.65f, 1f), new Color(0.2f, 0.65f, 1f));
        label.outlineWidth = 0.3f;
        label.outlineColor = new Color32(10, 25, 60, 255);
        label.sortingOrder = 90;
        return label;
    }

    /// <summary>Called by a target when its health reaches zero: it is now ready to be picked up.</summary>
    public void OnTargetReady(BoosterTarget target)
    {
        if (SoundManager.Instance != null)
            SoundManager.Instance.PlaySound(SoundName.HitEffect);
    }

    /// <summary>Called when the player runs into a ready target: load the 2X button.</summary>
    public void OnTargetCollected(BoosterTarget target)
    {
        targets.Remove(target);
        charges++;
        Loaded++;

        DamageNumbers.ShowText(target.transform.position + Vector3.up * 1.5f, "2X LOADED!", DamageNumbers.Style.Frost, 1.3f);

        if (SoundManager.Instance != null)
            SoundManager.Instance.PlaySound(SoundName.Upgrade);
    }

    // ---------------------------------------------------------------- button

    private void UpdateButton(DifficultySettings settings)
    {
        bool show = charges > 0 && ArcherGameState.IsGameRunning && Time.timeScale > 0f;

        if (show && buttonRoot == null)
            CreateButton(settings);

        if (buttonRoot == null)
            return;

        buttonRoot.SetActive(show);

        if (!show)
            return;

        // Gentle pulse so it gets noticed.
        float pulse = 1f + 0.06f * Mathf.Sin(Time.unscaledTime * 6f);
        buttonRect.localScale = Vector3.one * pulse;

        chargeBadge.gameObject.SetActive(charges > 1);
        chargeBadge.text = "x" + charges;
    }

    private void CreateButton(DifficultySettings settings)
    {
        buttonRoot = new GameObject("Booster Button Canvas");

        Canvas canvas = buttonRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;

        CanvasScaler scaler = buttonRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;
        buttonRoot.AddComponent<GraphicRaycaster>();

        GameObject buttonGO = new GameObject("Booster Button");
        buttonGO.transform.SetParent(buttonRoot.transform, false);
        buttonRect = buttonGO.AddComponent<RectTransform>();
        buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(1f, 0f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = new Vector2(-150f, 200f);   // below the archer, so it never covers him
        buttonRect.sizeDelta = new Vector2(190f, 229f);

        Image image = buttonGO.AddComponent<Image>();
        image.sprite = settings.boosterButtonSprite;
        image.preserveAspect = true;
        if (image.sprite == null)
            image.color = new Color(0.2f, 0.55f, 1f);

        Button button = buttonGO.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(OnButtonPressed);

        CreateUILabel(buttonRect, "2X", 92f, new Vector2(0f, 14f));
        CreateUILabel(buttonRect, "ARROWS", 34f, new Vector2(0f, -52f));

        chargeBadge = CreateUILabel(buttonRect, "", 40f, new Vector2(70f, 90f));
        chargeBadge.color = new Color(1f, 0.9f, 0.3f);
    }

    private static TextMeshProUGUI CreateUILabel(RectTransform parent, string text, float size, Vector2 position)
    {
        GameObject go = new GameObject("Text");
        go.transform.SetParent(parent, false);

        TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
        if (DifficultySettings.Current.damageNumberFont != null)
            label.font = DifficultySettings.Current.damageNumberFont;

        label.text = text;
        label.fontSize = size;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        label.outlineWidth = 0.25f;
        label.outlineColor = new Color32(10, 25, 60, 255);

        RectTransform rect = label.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(240f, 110f);
        rect.anchoredPosition = position;
        return label;
    }

    /// <summary>The 2X button: fire the boost volleys (current arrow count x multiplier, several times).</summary>
    public void OnButtonPressed()
    {
        if (charges <= 0 || !ArcherGameState.IsGameRunning)
            return;

        ArcherArrowController shooter = FindAnyObjectByType<ArcherArrowController>();
        if (shooter == null)
            return;

        DifficultySettings settings = DifficultySettings.Current;
        charges--;
        Fired++;

        int arrowsPerVolley = Mathf.Max(1, shooter.arrowsToShoot) * Mathf.Max(1, settings.boosterArrowMultiplier);
        shooter.FireBoostVolleys(Mathf.Max(1, settings.boosterVolleys), arrowsPerVolley, settings.boosterVolleyGap);

        DamageNumbers.ShowText(shooter.transform.position + Vector3.up * 2.5f, settings.boosterArrowMultiplier + "X!", DamageNumbers.Style.Frost, 1.5f);
    }
}

/// <summary>
/// A booster on the track. Far away it is locked (dim bar, arrows pass it by). Once it comes within
/// range, arrow hits drain its segmented health bar. At zero it turns READY (glowing, "GRAB IT!");
/// the player then has to run into it to collect it. If it passes the player it is lost.
/// </summary>
public class BoosterTarget : MonoBehaviour
{
    private enum State { Locked, Active, Ready }

    private BoosterPickup owner;
    private Transform visual;
    private Transform barRoot;
    private SpriteRenderer[] segments;
    private SpriteRenderer readyRing;
    private TextMeshPro title;
    private int health, maxHealth;
    private float spawnedAt, lastHitAt = -10f, readyAt = -1f;
    private Vector3 visualScale;
    private State state = State.Locked;

    public bool IsReady => state == State.Ready;

    private static readonly Color Full = new Color(0.25f, 0.85f, 1f);
    private static readonly Color Locked = new Color(0.45f, 0.55f, 0.65f, 0.55f);
    private static readonly Color Empty = new Color(0.12f, 0.16f, 0.3f);
    private static readonly Color Back = new Color(0.03f, 0.08f, 0.24f);
    private static readonly Color Gold = new Color(1f, 0.8f, 0.2f);

    private const float BarWidth = 1.5f;
    private const float BarHeight = 0.2f;
    private const float BarY = 2.1f;
    private const float PickupRadius = 1.4f;

    public void Init(BoosterPickup owner, Transform visual, int health)
    {
        this.owner = owner;
        this.visual = visual;
        this.health = maxHealth = health;
        visualScale = visual.localScale;
        spawnedAt = Time.time;
        BuildBar();
        SetSegmentColors(Locked);
    }

    private void BuildBar()
    {
        barRoot = new GameObject("Health Bar").transform;
        barRoot.SetParent(transform, false);
        barRoot.localPosition = new Vector3(0f, BarY, 0f);

        SpriteRenderer back = NewQuad(barRoot, "Back", Back, 95);
        back.transform.localScale = new Vector3(BarWidth + 0.08f, BarHeight + 0.08f, 1f);

        segments = new SpriteRenderer[maxHealth];
        const float gap = 0.03f;
        float segWidth = (BarWidth - gap * (maxHealth - 1)) / maxHealth;

        for (int i = 0; i < maxHealth; i++)
        {
            SpriteRenderer seg = NewQuad(barRoot, "Segment", Full, 96);
            seg.transform.localScale = new Vector3(segWidth, BarHeight, 1f);
            seg.transform.localPosition = new Vector3(-BarWidth / 2f + segWidth / 2f + i * (segWidth + gap), 0f, 0f);
            segments[i] = seg;
        }

        title = BoosterPickup.CreateWorldLabel(transform, "2X", 5.5f);
        title.transform.localPosition = new Vector3(0f, BarY + 0.5f, 0f);

        // Flat glowing ring on the ground, shown once the booster is ready to grab.
        readyRing = NewQuad(transform, "Ready Ring", Gold, 4);
        readyRing.transform.localPosition = new Vector3(0f, 0.03f, 0f);
        readyRing.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        readyRing.transform.localScale = new Vector3(2.2f, 2.2f, 1f);
        readyRing.sprite = RingSprite;
        readyRing.gameObject.SetActive(false);
    }

    private static Sprite ringSprite;

    // Soft gold ring texture, generated once.
    private static Sprite RingSprite
    {
        get
        {
            if (ringSprite != null)
                return ringSprite;

            const int size = 64;
            Texture2D tex = new Texture2D(size, size);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(size / 2f - 0.5f, size / 2f - 0.5f)) / (size / 2f);
                    float a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.8f) / 0.18f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            tex.Apply();
            ringSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return ringSprite;
        }
    }

    private static SpriteRenderer NewQuad(Transform parent, string name, Color color, int order)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = ModelUtil.WhiteSprite;
        sr.color = color;
        sr.sortingOrder = order;
        return sr;
    }

    private void SetSegmentColors(Color color)
    {
        for (int i = 0; i < segments.Length; i++)
            segments[i].color = i < health ? color : Empty;
    }

    private void Update()
    {
        Transform player = EnemySpawner.FindPlayerTarget();
        float t = Time.time - spawnedAt;

        // Locked -> Active once it comes within range.
        if (state == State.Locked && player != null &&
            transform.position.z - player.position.z <= DifficultySettings.Current.boosterDamageRange)
        {
            state = State.Active;
            SetSegmentColors(Full);
        }

        if (state == State.Ready)
        {
            // Bounce and glow until the player runs into it.
            float bounce = Mathf.Abs(Mathf.Sin((Time.time - readyAt) * 6f)) * 0.35f;
            visual.localPosition = new Vector3(0f, bounce, 0f);
            visual.localScale = visualScale * (1.1f + 0.05f * Mathf.Sin(Time.time * 10f));

            float pulse = 0.6f + 0.4f * Mathf.Sin(Time.time * 8f);
            readyRing.color = new Color(Gold.r, Gold.g, Gold.b, pulse);
            readyRing.transform.localScale = Vector3.one * (2.2f + 0.25f * pulse);
            title.transform.localScale = Vector3.one * (1f + 0.12f * Mathf.Sin(Time.time * 10f));

            if (player != null && ArcherGameState.IsGameRunning)
            {
                Vector3 offset = transform.position - player.position;
                offset.y = 0f;

                if (offset.magnitude <= PickupRadius)
                {
                    owner.OnTargetCollected(this);
                    Destroy(gameObject);
                    return;
                }
            }
        }
        else
        {
            // Gentle bob and a punch when hit.
            float sinceHit = Time.time - lastHitAt;
            float punch = Mathf.Clamp01(1f - sinceHit / 0.15f) * 0.2f;
            visual.localScale = visualScale * (1f + punch);
            visual.localPosition = new Vector3(0f, 0.06f * Mathf.Sin(t * 3f), 0f);

            // Segment that was just lost flashes white, then goes dark.
            if (health < maxHealth && health >= 0)
                segments[health].color = sinceHit < 0.12f ? Color.white : Empty;
        }

        Camera cam = Camera.main;
        if (cam != null)
        {
            barRoot.rotation = cam.transform.rotation;
            title.transform.rotation = cam.transform.rotation;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Only takes damage while active (in range and not yet ready). Otherwise arrows fly past.
        if (state != State.Active || !ArcherGameState.IsGameRunning)
            return;

        ArrowController arrow = other.GetComponent<ArrowController>();
        if (arrow == null || arrow.IsSpent)
            return;

        arrow.ReturnToPool();

        // Earlier lost segments settle to dark; the newest one flashes in Update.
        if (health < maxHealth)
            segments[health].color = Empty;

        health--;
        lastHitAt = Time.time;

        if (health <= 0)
            BecomeReady();
    }

    private void BecomeReady()
    {
        state = State.Ready;
        readyAt = Time.time;

        barRoot.gameObject.SetActive(false);
        readyRing.gameObject.SetActive(true);

        title.text = "GRAB IT!";
        title.fontSize = 5f;
        title.colorGradient = new VertexGradient(new Color(1f, 1f, 0.6f), new Color(1f, 1f, 0.6f), new Color(1f, 0.6f, 0.05f), new Color(1f, 0.6f, 0.05f));
        title.transform.localPosition = new Vector3(0f, 1.9f, 0f);

        owner.OnTargetReady(this);
    }
}
