using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Coin icons that fly in an arc from a point in the world to the coin counter in the HUD,
/// making the counter "pop" as each one lands. Creates itself on first use.
/// </summary>
public class CoinFlyFX : MonoBehaviour
{
    private class FlyingCoin
    {
        public RectTransform rect;
        public Vector3 start, control;
        public float delay, duration, age;
    }

    private static CoinFlyFX instance;

    private readonly List<FlyingCoin> coins = new List<FlyingCoin>();
    private Canvas canvas;
    private RectTransform target;
    private Sprite coinSprite;
    private float targetPunch;
    private Vector3 targetBaseScale = Vector3.one;

    public static void Fly(Vector3 worldFrom, int count)
    {
        if (!DifficultySettings.Current.enabled || !DifficultySettings.Current.coinFlyToCounter)
            return;

        if (instance == null)
            instance = new GameObject("Coin Fly FX").AddComponent<CoinFlyFX>();

        instance.Launch(worldFrom, count);
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
        if (target != null)
            target.localScale = targetBaseScale;
    }

    private bool FindTarget()
    {
        if (target != null && canvas != null)
            return true;

        // The coin counter text from MainMenuUIHandler; prefer the gold coin icon next to it.
        MainMenuUIHandler ui = MainMenuUIHandler.Instance;
        if (ui == null || ui.coinTxt == null)
            return false;

        foreach (TMP_Text text in ui.coinTxt)
        {
            if (text == null || !text.gameObject.activeInHierarchy)
                continue;

            target = (RectTransform)text.transform;
            Transform bar = text.transform.parent;
            if (bar != null)
                foreach (Image image in bar.GetComponentsInChildren<Image>())
                    if (image.sprite != null && image.sprite.name.ToLower().Contains("coin"))
                    {
                        target = image.rectTransform;
                        coinSprite = image.sprite;
                        break;
                    }

            canvas = target.GetComponentInParent<Canvas>().rootCanvas;
            targetBaseScale = target.localScale;
            return true;
        }

        return false;
    }

    private void Launch(Vector3 worldFrom, int count)
    {
        Camera cam = Camera.main;
        if (cam == null || !FindTarget())
            return;

        Vector3 screenFrom = cam.WorldToScreenPoint(worldFrom);
        float scale = canvas.scaleFactor;

        for (int i = 0; i < count; i++)
        {
            GameObject go = new GameObject("Flying Coin", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(canvas.transform, false);
            go.transform.SetAsLastSibling();

            Image image = go.GetComponent<Image>();
            image.sprite = coinSprite;
            image.raycastTarget = false;
            if (coinSprite == null)
                image.color = new Color(1f, 0.8f, 0.2f);

            RectTransform rect = image.rectTransform;
            rect.sizeDelta = new Vector2(70f, 70f);

            // Burst out a little around the barrel, then arc up toward the counter.
            Vector3 start = screenFrom + (Vector3)(Random.insideUnitCircle * 60f * scale);
            Vector3 control = start + new Vector3(Random.Range(-220f, 220f), Random.Range(180f, 380f), 0f) * scale;
            rect.position = start;

            coins.Add(new FlyingCoin
            {
                rect = rect,
                start = start,
                control = control,
                delay = i * 0.05f,
                duration = Random.Range(0.55f, 0.8f),
            });
        }
    }

    private void Update()
    {
        for (int i = coins.Count - 1; i >= 0; i--)
        {
            FlyingCoin coin = coins[i];

            if (coin.rect == null || target == null)
            {
                if (coin.rect != null) Destroy(coin.rect.gameObject);
                coins.RemoveAt(i);
                continue;
            }

            coin.age += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01((coin.age - coin.delay) / coin.duration);
            float eased = t * t;   // speeds up into the counter

            Vector3 end = target.position;
            Vector3 a = Vector3.Lerp(coin.start, coin.control, eased);
            Vector3 b = Vector3.Lerp(coin.control, end, eased);
            coin.rect.position = Vector3.Lerp(a, b, eased);
            coin.rect.localScale = Vector3.one * Mathf.Lerp(1.15f, 0.65f, eased);

            if (t >= 1f)
            {
                targetPunch = 1f;
                Destroy(coin.rect.gameObject);
                coins.RemoveAt(i);
            }
        }

        // Counter "pop" each time a coin lands.
        if (target != null)
        {
            targetPunch = Mathf.MoveTowards(targetPunch, 0f, Time.unscaledDeltaTime * 6f);
            target.localScale = targetBaseScale * (1f + 0.25f * targetPunch);
        }
    }
}
