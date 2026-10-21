using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Every so often (before the boss) a bundle of coins appears on the ground ahead of the player.
/// Steering into the bundle collects all of it at once. Bundles ride on the moving platform.
/// Created by EnemySpawner.
/// </summary>
public class CoinTrail : MonoBehaviour
{
    public static CoinTrail Instance { get; private set; }

    /// <summary>Coins spawned / picked up this session (for playtest stats).</summary>
    public static int Spawned, Collected;

    private class Bundle
    {
        public Transform root;
        public Transform bucket;
        public readonly List<Transform> coins = new List<Transform>();
        public float collectedAt = -1f;
        public float phase;
    }

    private static Material rimMaterial, faceMaterial;

    private readonly List<Bundle> bundles = new List<Bundle>();
    private float nextBundle = -1f;

    private const float CoinSize = 0.5f;
    private const float PickupRadius = 1.3f;
    private const float CollectAnimTime = 0.3f;
    private const float CollectStagger = 0.04f;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        DifficultySettings settings = DifficultySettings.Current;
        Transform player = EnemySpawner.FindPlayerTarget();

        if (!settings.CoinsActive || player == null)
            return;

        if (ArcherGameState.IsGameRunning && !EnemySpawner.BossTriggered)
        {
            if (nextBundle < 0f)
                nextBundle = Time.time + 3f;

            if (Time.time >= nextBundle)
            {
                nextBundle = Time.time + settings.coinBundleInterval;
                SpawnBundle(settings, player);
            }
        }

        // Boss fight: the ground stops, so clear leftover bundles (no reward).
        if (EnemySpawner.BossTriggered)
        {
            foreach (Bundle bundle in bundles)
                if (bundle.collectedAt < 0f)
                    bundle.collectedAt = Time.time;
        }

        AnimateAndCollect(settings, player);
    }

    private void SpawnBundle(DifficultySettings settings, Transform player)
    {
        PlatformMover platform = EnemySpawner.FindPlatformMover();
        float y = EnemySpawner.GroundY;
        float z = DifficultySettings.ScreenTopGroundZ(EnemySpawner.GroundY, player.position.z + 16f) + 1.5f;
        float x = Random.Range(-2.4f, 2.4f);

        Bundle bundle = new Bundle { root = new GameObject("Coin Bundle").transform, phase = Random.Range(0f, 360f) };
        bundle.root.position = new Vector3(x, y, z);

        // Ride along with the scrolling ground.
        if (platform != null)
            bundle.root.SetParent(platform.transform, true);

        int count = Mathf.Max(1, settings.coinsPerBundle);

        // A bucket overflowing with coins (falls back to a loose pile if the model is missing).
        bundle.bucket = ModelUtil.InstantiateWithHeight("Kenney/Survival/barrel-open", 0.8f, "Coin Bucket");
        if (bundle.bucket != null)
            bundle.bucket.SetParent(bundle.root, false);

        float pileBase = bundle.bucket != null ? 0.74f : 0.3f;

        for (int i = 0; i < count; i++)
        {
            Transform coin = CreateCoin().transform;
            coin.SetParent(bundle.root, false);

            // A mound: one coin on top in the middle, the rest in a ring, tilted every which way.
            if (i == 0)
            {
                coin.localPosition = new Vector3(0f, pileBase + 0.22f, 0f);
            }
            else
            {
                float angle = (i - 1) * Mathf.PI * 2f / (count - 1);
                coin.localPosition = new Vector3(Mathf.Cos(angle) * 0.2f, pileBase + Random.Range(0f, 0.08f), Mathf.Sin(angle) * 0.2f);
            }

            coin.localRotation = Quaternion.Euler(Random.Range(-60f, 60f), Random.Range(0f, 360f), Random.Range(-30f, 30f));
            bundle.coins.Add(coin);
        }

        bundles.Add(bundle);
        Spawned += count;
    }

    /// <summary>A 3D gold coin: the Kenney coin model, or a built gold disc if the model is missing.</summary>
    private static GameObject CreateCoin()
    {
        Transform kenney = ModelUtil.InstantiateWithHeight("Kenney/Dungeon/coin", CoinSize * 0.6f, "Coin");
        if (kenney != null)
            return kenney.gameObject;

        if (rimMaterial == null)
        {
            rimMaterial = Resources.Load<Material>("CoinGoldRim");
            faceMaterial = Resources.Load<Material>("CoinGoldFace");
        }

        GameObject root = new GameObject("Coin");
        root.transform.localScale = Vector3.one * CoinSize * 0.6f;

        GameObject rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(rim.GetComponent<Collider>());
        rim.transform.SetParent(root.transform, false);
        rim.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // stand the disc up
        rim.transform.localScale = new Vector3(1f, 0.08f, 1f);

        GameObject face = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(face.GetComponent<Collider>());
        face.transform.SetParent(root.transform, false);
        face.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        face.transform.localScale = new Vector3(0.72f, 0.1f, 0.72f);

        foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        if (rimMaterial != null) rim.GetComponent<Renderer>().sharedMaterial = rimMaterial;
        if (faceMaterial != null) face.GetComponent<Renderer>().sharedMaterial = faceMaterial;

        return root;
    }

    private void AnimateAndCollect(DifficultySettings settings, Transform player)
    {
        for (int b = bundles.Count - 1; b >= 0; b--)
        {
            Bundle bundle = bundles[b];

            if (bundle.root == null)
            {
                bundles.RemoveAt(b);
                continue;
            }

            // Collected: coins burst upward one after another, spinning and shrinking away.
            if (bundle.collectedAt >= 0f)
            {
                bool allDone = true;

                for (int i = 0; i < bundle.coins.Count; i++)
                {
                    float t = (Time.time - bundle.collectedAt - i * CollectStagger) / CollectAnimTime;

                    if (t < 0f) { allDone = false; continue; }
                    if (t >= 1f) { bundle.coins[i].localScale = Vector3.zero; continue; }

                    allDone = false;
                    Transform coin = bundle.coins[i];
                    coin.Rotate(0f, 1440f * Time.deltaTime, 0f, Space.World);
                    coin.position += Vector3.up * 7f * Time.deltaTime;
                    coin.localScale = Vector3.one * Mathf.Lerp(1.3f, 0f, t * t);
                }

                if (bundle.bucket != null)
                {
                    float bt = Mathf.Clamp01((Time.time - bundle.collectedAt) / CollectAnimTime);
                    bundle.bucket.localScale = Vector3.one * Mathf.Lerp(1.15f, 0f, bt * bt);
                }

                if (allDone)
                {
                    Destroy(bundle.root.gameObject);
                    bundles.RemoveAt(b);
                }
                continue;
            }

            // Idle: the bucket turns slowly so the coins catch the light.
            bundle.root.rotation = Quaternion.Euler(0f, Time.time * 60f + bundle.phase, 0f);

            Vector3 offset = bundle.root.position - player.position;
            offset.y = 0f;

            if (ArcherGameState.IsGameRunning && offset.magnitude <= PickupRadius)
            {
                bundle.collectedAt = Time.time;
                Collected += bundle.coins.Count;

                // One reward (and one coin sound / popup) for the whole bundle.
                if (MainMenuUIHandler.Instance != null)
                    MainMenuUIHandler.Instance.AddToReward(settings.coinValue * bundle.coins.Count);
                continue;
            }

            // Passed behind the player: remove.
            if (bundle.root.position.z < player.position.z - 3f)
            {
                Destroy(bundle.root.gameObject);
                bundles.RemoveAt(b);
            }
        }
    }
}
