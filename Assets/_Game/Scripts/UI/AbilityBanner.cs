using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// When the player runs through an ability gate: the ability's icon and name pop up in the middle of the
/// screen, then the icon shrinks into a badge in the bottom-left corner that stays while the ability is active.
/// Losing the ability (a "remove" gate) shows the crossed-out icon and hides the badge.
/// Built at runtime under Canvas > Gameplay (DifficultySettings.abilityBanner); nothing is changed in the scene.
/// </summary>
public class AbilityBanner : MonoBehaviour
{
    // Canvas units (the gameplay canvas is 1920 wide).
    private const float IconSize = 380f;
    private const float BadgeSize = 230f;
    private static readonly Vector2 BannerPos = new Vector2(0f, -800f);   // under the progress bar
    private static readonly Vector2 BadgePos = new Vector2(190f, 230f);   // from the bottom-left corner

    private const float PopTime = 0.25f;
    private const float HoldTime = 0.9f;
    private const float FlyTime = 0.35f;

    private static AbilityBanner instance;

    private RectTransform bannerIcon;
    private Image bannerImage;
    private TextMeshProUGUI title;
    private RectTransform badge;
    private Image badgeImage;
    private Coroutine running;
    private Sprite pendingBadge;   // icon heading for the badge while the banner is up

    /// <param name="icon">The gate's icon (its "on" sprite, or "off" when the ability was removed).</param>
    public static void Show(ArrowType ability, Sprite icon, bool removed)
    {
        if (!DifficultySettings.Current.AbilityBannerActive)
            return;

        if (instance == null && !Create())
            return;

        instance.Play(ability, icon, removed);
    }

    private static bool Create()
    {
        MainMenuUIHandler ui = MainMenuUIHandler.Instance;
        if (ui == null || ui.gameplayObj == null)
            return false;

        GameObject root = new GameObject("Ability Banner", typeof(RectTransform));
        RectTransform rect = (RectTransform)root.transform;
        rect.SetParent(ui.gameplayObj.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;

        instance = root.AddComponent<AbilityBanner>();
        instance.Build();
        return true;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Build()
    {
        badgeImage = NewImage("Badge", transform);
        badge = badgeImage.rectTransform;
        badge.anchorMin = badge.anchorMax = new Vector2(0f, 0f);
        badge.sizeDelta = Vector2.one * BadgeSize;
        badge.anchoredPosition = BadgePos;
        badge.gameObject.SetActive(false);

        bannerImage = NewImage("Icon", transform);
        bannerIcon = bannerImage.rectTransform;
        bannerIcon.anchorMin = bannerIcon.anchorMax = new Vector2(0.5f, 1f);
        bannerIcon.sizeDelta = Vector2.one * IconSize;
        bannerIcon.gameObject.SetActive(false);

        title = new GameObject("Title", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        title.transform.SetParent(transform, false);
        if (DifficultySettings.Current.damageNumberFont != null)
            title.font = DifficultySettings.Current.damageNumberFont;
        title.fontSize = 120f;
        title.alignment = TextAlignmentOptions.Center;
        title.enableWordWrapping = false;
        title.raycastTarget = false;
        title.outlineWidth = 0.25f;
        title.outlineColor = new Color32(10, 25, 60, 255);
        RectTransform t = title.rectTransform;
        t.anchorMin = t.anchorMax = new Vector2(0.5f, 1f);
        t.sizeDelta = new Vector2(1600f, 160f);
        t.anchoredPosition = BannerPos + new Vector2(0f, -IconSize * 0.5f - 70f);
        title.gameObject.SetActive(false);
    }

    private void Play(ArrowType ability, Sprite icon, bool removed)
    {
        Finish();

        bannerImage.sprite = icon;
        bannerImage.enabled = icon != null;
        title.text = removed ? Name(ability) + " LOST" : Name(ability) + "!";
        Color[] g = removed ? new[] { new Color(1f, 0.6f, 0.6f), new Color(0.85f, 0.15f, 0.15f) } : Colors(ability);
        title.color = Color.white;
        title.enableVertexGradient = true;
        title.colorGradient = new VertexGradient(g[0], g[0], g[1], g[1]);

        // The old badge goes; a real ability flies into a new one (normal arrows and removals leave it empty).
        HideBadge();
        bool keepBadge = !removed && ability != ArrowType.SimpleArrow && icon != null;
        pendingBadge = keepBadge ? icon : null;

        if (isActiveAndEnabled)
            running = StartCoroutine(Animate(icon, keepBadge));
        else
            Finish();
    }

    private IEnumerator Animate(Sprite icon, bool toBadge)
    {
        bannerIcon.gameObject.SetActive(true);
        title.gameObject.SetActive(true);
        bannerIcon.anchoredPosition = BannerPos;
        bannerIcon.anchorMin = bannerIcon.anchorMax = new Vector2(0.5f, 1f);
        SetAlpha(1f);

        // Pop in with a small overshoot.
        for (float t = 0f; t < 1f; t += Time.deltaTime / PopTime)
        {
            float s = BackOut(t);
            bannerIcon.localScale = Vector3.one * s;
            title.rectTransform.localScale = Vector3.one * s;
            yield return null;
        }
        bannerIcon.localScale = title.rectTransform.localScale = Vector3.one;

        yield return new WaitForSeconds(HoldTime);

        if (toBadge)
        {
            // Fly the icon into the corner while the title fades.
            Vector3 from = bannerIcon.position;
            Vector3 to = BadgeWorldPosition();
            float endScale = BadgeSize / IconSize;

            for (float t = 0f; t < 1f; t += Time.deltaTime / FlyTime)
            {
                float e = t * t * (3f - 2f * t);
                bannerIcon.position = Vector3.Lerp(from, to, e);
                bannerIcon.localScale = Vector3.one * Mathf.Lerp(1f, endScale, e);
                title.alpha = 1f - t;
                yield return null;
            }

            HideBanner();
            ShowBadge(icon);
            pendingBadge = null;

            // Little bump when it lands.
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.2f)
            {
                badge.localScale = Vector3.one * (1f + 0.2f * Mathf.Sin(t * Mathf.PI));
                yield return null;
            }
            badge.localScale = Vector3.one;
        }
        else
        {
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.3f)
            {
                SetAlpha(1f - t);
                yield return null;
            }

            HideBanner();
        }

        running = null;
    }

    // If the gameplay UI is hidden mid-animation, jump to the end state.
    private void OnDisable() => Finish();

    private void Finish()
    {
        if (running != null)
        {
            StopCoroutine(running);
            running = null;
        }

        if (pendingBadge != null)
        {
            ShowBadge(pendingBadge);
            pendingBadge = null;
        }

        HideBanner();
        badge.localScale = Vector3.one;
    }

    private Vector3 BadgeWorldPosition()
    {
        bool wasActive = badge.gameObject.activeSelf;
        badge.gameObject.SetActive(true);
        Vector3 p = badge.position;
        badge.gameObject.SetActive(wasActive);
        return p;
    }

    private void ShowBadge(Sprite icon)
    {
        badgeImage.sprite = icon;
        badge.gameObject.SetActive(true);
    }

    private void HideBadge()
    {
        badge.gameObject.SetActive(false);
    }

    private void HideBanner()
    {
        bannerIcon.gameObject.SetActive(false);
        title.gameObject.SetActive(false);
        bannerIcon.localScale = title.rectTransform.localScale = Vector3.one;
        SetAlpha(1f);
    }

    private void SetAlpha(float a)
    {
        bannerImage.color = new Color(1f, 1f, 1f, a);
        title.alpha = a;
    }

    private static float BackOut(float t)
    {
        const float c = 1.70158f;
        t -= 1f;
        return 1f + (c + 1f) * t * t * t + c * t * t;
    }

    private static string Name(ArrowType ability)
    {
        switch (ability)
        {
            case ArrowType.FireArrow: return "FIRE ARROWS";
            case ArrowType.FreezeArrow: return "FREEZE ARROWS";
            case ArrowType.ReversedArrow: return "REVERSE ARROWS";
            case ArrowType.ExpandAngleArrow: return "WIDE SHOT";
            default: return "NORMAL ARROWS";
        }
    }

    // Top/bottom gradient per ability, matching the icon colours.
    private static Color[] Colors(ArrowType ability)
    {
        switch (ability)
        {
            case ArrowType.FireArrow: return new[] { new Color(1f, 0.85f, 0.3f), new Color(1f, 0.3f, 0.05f) };
            case ArrowType.FreezeArrow: return new[] { new Color(0.85f, 1f, 1f), new Color(0.2f, 0.65f, 1f) };
            case ArrowType.ReversedArrow: return new[] { new Color(0.9f, 0.75f, 1f), new Color(0.6f, 0.3f, 1f) };
            case ArrowType.ExpandAngleArrow: return new[] { new Color(0.8f, 1f, 0.6f), new Color(0.25f, 0.8f, 0.2f) };
            default: return new[] { new Color(1f, 0.97f, 0.45f), new Color(1f, 0.55f, 0.08f) };
        }
    }

    private static Image NewImage(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }
}
