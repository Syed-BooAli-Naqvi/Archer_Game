using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pops a panel in every time it is enabled: the panel fades in, its window bounces up to size and
/// its buttons pop in one after another. Uses unscaled time so it also plays while the game is paused.
/// Added at runtime by MainMenuUIHandler (DifficultySettings.panelAnimations).
/// </summary>
public class PanelAnimator : MonoBehaviour
{
    private const float FadeTime = 0.18f;
    private const float PopTime = 0.35f;
    private const float ButtonDelay = 0.12f;
    private const float ButtonStagger = 0.06f;
    private const float ButtonPopTime = 0.25f;

    private CanvasGroup group;
    private readonly List<RectTransform> windows = new();
    private readonly List<RectTransform> buttons = new();
    private readonly Dictionary<RectTransform, Vector3> baseScales = new();

    public static void Attach(GameObject panel)
    {
        if (panel != null && panel.GetComponent<PanelAnimator>() == null)
            panel.AddComponent<PanelAnimator>();
    }

    private void Collect()
    {
        group = GetComponent<CanvasGroup>();
        if (group == null)
            group = gameObject.AddComponent<CanvasGroup>();

        RectTransform root = transform as RectTransform;
        float rootArea = root != null ? Mathf.Abs(root.rect.width * root.rect.height) : 0f;

        // Children noticeably smaller than the panel are the "window"; full-screen dimmers only fade.
        foreach (Transform child in transform)
        {
            if (child is RectTransform rt && rootArea > 0f && Mathf.Abs(rt.rect.width * rt.rect.height) < rootArea * 0.9f)
                Remember(rt, windows);
        }

        if (windows.Count == 0 && root != null && rootArea > 0f && !IsFullScreen(root))
            Remember(root, windows);

        foreach (Button button in GetComponentsInChildren<Button>(true))
        {
            RectTransform rt = button.transform as RectTransform;

            // Buttons inside layout groups would fight the layout; leave them to the window pop.
            if (rt != null && !windows.Contains(rt) && rt.parent.GetComponent<LayoutGroup>() == null)
                Remember(rt, buttons);
        }
    }

    private bool IsFullScreen(RectTransform rt)
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            return true;

        Rect c = ((RectTransform)canvas.rootCanvas.transform).rect;
        return rt.rect.width >= c.width * 0.95f && rt.rect.height >= c.height * 0.95f;
    }

    private void Remember(RectTransform rt, List<RectTransform> list)
    {
        list.Add(rt);
        if (!baseScales.ContainsKey(rt))
            baseScales[rt] = rt.localScale;
    }

    private void OnEnable()
    {
        DifficultySettings settings = DifficultySettings.Current;
        if (settings != null && !settings.PanelAnimationsActive)
            return;

        if (group == null)
            Collect();

        StopAllCoroutines();
        StartCoroutine(Play());
    }

    private void OnDisable()
    {
        // Leave everything at full size so the panel looks right if animations get switched off.
        if (group != null)
            group.alpha = 1f;

        foreach (var pair in baseScales)
        {
            if (pair.Key != null)
                pair.Key.localScale = pair.Value;
        }
    }

    private IEnumerator Play()
    {
        group.alpha = 0f;
        foreach (RectTransform w in windows) w.localScale = baseScales[w] * 0.75f;
        foreach (RectTransform b in buttons) b.localScale = Vector3.zero;

        float t = 0f;
        float total = Mathf.Max(PopTime, ButtonDelay + ButtonStagger * buttons.Count + ButtonPopTime);

        while (t < total)
        {
            t += Time.unscaledDeltaTime;

            group.alpha = Mathf.Clamp01(t / FadeTime);

            float pop = EaseOutBack(Mathf.Clamp01(t / PopTime));
            foreach (RectTransform w in windows)
                w.localScale = baseScales[w] * Mathf.LerpUnclamped(0.75f, 1f, pop);

            for (int i = 0; i < buttons.Count; i++)
            {
                float k = Mathf.Clamp01((t - ButtonDelay - ButtonStagger * i) / ButtonPopTime);
                buttons[i].localScale = baseScales[buttons[i]] * EaseOutBack(k);
            }

            yield return null;
        }

        group.alpha = 1f;
        foreach (var pair in baseScales)
            pair.Key.localScale = pair.Value;
    }

    public static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }

    /// <summary>Quick scale punch on any transform (e.g. the "GO!" text).</summary>
    public static IEnumerator Punch(Transform target, float from = 1.8f, float duration = 0.35f)
    {
        if (target == null)
            yield break;

        Vector3 baseScale = target.localScale;
        float t = 0f;

        while (t < duration && target != null)
        {
            t += Time.unscaledDeltaTime;
            target.localScale = baseScale * Mathf.Lerp(from, 1f, EaseOutBack(Mathf.Clamp01(t / duration)));
            yield return null;
        }

        if (target != null)
            target.localScale = baseScale;
    }
}

/// <summary>
/// Slides the main-menu pieces in from the nearest top/bottom edge, one after another, and gently
/// pulses the play button so it reads as the thing to press.
/// </summary>
public class MenuIntroAnimator : MonoBehaviour
{
    public Transform pulseTarget;

    private readonly List<RectTransform> items = new();
    private readonly List<Vector2> basePositions = new();
    private bool collected;
    private Vector3 pulseBase;

    public static void Attach(GameObject menu, Transform pulse)
    {
        if (menu == null || menu.GetComponent<MenuIntroAnimator>() != null)
            return;

        MenuIntroAnimator intro = menu.AddComponent<MenuIntroAnimator>();
        if (pulse == null)
            return;

        intro.pulseTarget = pulse;
        intro.pulseBase = pulse.localScale;

        DifficultySettings settings = DifficultySettings.Current;
        if (intro.isActiveAndEnabled && (settings == null || settings.PanelAnimationsActive))
            intro.StartCoroutine(intro.Pulse());
    }

    private void Collect()
    {
        collected = true;

        if (GetComponent<LayoutGroup>() != null)
            return;

        foreach (Transform child in transform)
        {
            if (child is RectTransform rt && child.gameObject.activeSelf)
            {
                items.Add(rt);
                basePositions.Add(rt.anchoredPosition);
            }
        }
    }

    private void OnEnable()
    {
        DifficultySettings settings = DifficultySettings.Current;
        if (settings != null && !settings.PanelAnimationsActive)
            return;

        if (!collected)
            Collect();

        StopAllCoroutines();
        StartCoroutine(Intro());
        if (pulseTarget != null)
            StartCoroutine(Pulse());
    }

    private void OnDisable()
    {
        if (pulseTarget != null && collected)
            pulseTarget.localScale = pulseBase;

        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] != null)
                items[i].anchoredPosition = basePositions[i];
        }
    }

    private IEnumerator Intro()
    {
        const float slide = 0.45f, stagger = 0.05f, distance = 450f;
        Vector2[] offsets = new Vector2[items.Count];

        for (int i = 0; i < items.Count; i++)
        {
            // Pieces on the top half come down from above, the rest come up from below.
            Vector3 world = items[i].TransformPoint(items[i].rect.center);
            Vector3 local = transform.InverseTransformPoint(world);
            offsets[i] = new Vector2(0f, local.y >= 0f ? distance : -distance);
            items[i].anchoredPosition = basePositions[i] + offsets[i];
        }

        float t = 0f, total = slide + stagger * items.Count;

        while (t < total)
        {
            t += Time.unscaledDeltaTime;

            for (int i = 0; i < items.Count; i++)
            {
                float k = PanelAnimator.EaseOutBack(Mathf.Clamp01((t - stagger * i) / slide));
                items[i].anchoredPosition = basePositions[i] + offsets[i] * (1f - k);
            }

            yield return null;
        }

        for (int i = 0; i < items.Count; i++)
            items[i].anchoredPosition = basePositions[i];
    }

    private IEnumerator Pulse()
    {
        while (pulseTarget != null)
        {
            float s = 1f + 0.06f * Mathf.Sin(Time.unscaledTime * 4f);
            pulseTarget.localScale = pulseBase * s;
            yield return null;
        }
    }
}
