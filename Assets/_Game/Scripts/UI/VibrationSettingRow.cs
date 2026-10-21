using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Adds a "Vibration" row with an on/off switch under the Music row of the settings panel, built at
/// runtime from a copy of that row (DifficultySettings.vibrationSetting); nothing is changed in the scene.
/// </summary>
public class VibrationSettingRow : MonoBehaviour
{
    private const float SwitchScale = 1.2f;
    private const float HandleX = 43.1f * SwitchScale;

    private Image background;
    private Image handle;
    private Coroutine slide;

    /// <summary>Builds the row next to the row that holds the music slider.</summary>
    public static void Build(Slider musicSlider)
    {
        DifficultySettings settings = DifficultySettings.Current;
        if (settings == null || !settings.VibrationSettingActive || musicSlider == null)
            return;

        Transform musicRow = musicSlider.transform.parent;
        Transform group = musicRow.parent;

        // Divider line between rows, same as between Sound and Music.
        Transform line = group.Find("Line");
        if (line != null)
            Instantiate(line.gameObject, group).name = "Line (Vibration)";

        GameObject row = Instantiate(musicRow.gameObject, group);
        row.name = "Vibration";

        // Drop the copied slider; the switch goes in its place.
        Slider copiedSlider = row.GetComponentInChildren<Slider>(true);
        if (copiedSlider != null)
            DestroyImmediate(copiedSlider.gameObject);

        TMP_Text label = row.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
            label.text = "Vibration";

        Transform icon = row.transform.Find("Icon");
        if (icon != null && settings.vibrationIcon != null && icon.TryGetComponent(out Image iconImage))
            iconImage.sprite = settings.vibrationIcon;

        row.AddComponent<VibrationSettingRow>().BuildSwitch(settings);
    }

    private void BuildSwitch(DifficultySettings settings)
    {
        this.settings = settings;

        background = NewImage("Switch", transform, settings.switchOffSprite);
        background.type = Image.Type.Sliced;
        RectTransform bg = background.rectTransform;
        bg.anchorMin = bg.anchorMax = new Vector2(1f, 0.5f);
        bg.pivot = new Vector2(1f, 0.5f);
        bg.sizeDelta = new Vector2(177.5f, 65f) * SwitchScale;
        bg.anchoredPosition = new Vector2(-60f, 0f);

        handle = NewImage("Handle", bg, settings.switchHandleOffSprite);
        handle.raycastTarget = false;
        handle.rectTransform.sizeDelta = new Vector2(96f, 115f) * SwitchScale;

        Button button = background.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(Toggle);

        Show(Haptics.Enabled, false);
    }

    private DifficultySettings settings;

    private void Toggle()
    {
        bool on = !Haptics.Enabled;
        Haptics.Enabled = on;

        if (SoundManager.Instance != null)
            SoundManager.Instance.PlaySound(SoundName.ButtonClick);

        // A buzz when switched on, so the player feels what they turned on.
        if (on)
            Haptics.Play(HapticType.Medium);

        Show(on, true);
    }

    private void Show(bool on, bool animate)
    {
        background.sprite = on ? settings.switchOnSprite : settings.switchOffSprite;
        handle.sprite = on ? settings.switchHandleOnSprite : settings.switchHandleOffSprite;

        // Fallback colours if the sprites aren't set.
        if (background.sprite == null)
            background.color = on ? new Color(1f, 0.65f, 0.1f) : new Color(0.35f, 0.35f, 0.4f);
        if (handle.sprite == null)
            handle.color = Color.white;

        Vector2 target = new Vector2(on ? HandleX : -HandleX, -5.4f * SwitchScale);

        if (slide != null)
            StopCoroutine(slide);

        if (animate && isActiveAndEnabled)
            slide = StartCoroutine(Slide(target));
        else
            handle.rectTransform.anchoredPosition = target;
    }

    private IEnumerator Slide(Vector2 target)
    {
        RectTransform rect = handle.rectTransform;
        Vector2 start = rect.anchoredPosition;

        // Unscaled time: the settings panel can be opened while the game is paused.
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.12f)
        {
            rect.anchoredPosition = Vector2.Lerp(start, target, 1f - (1f - t) * (1f - t));
            yield return null;
        }

        rect.anchoredPosition = target;
        slide = null;
    }

    private static Image NewImage(string name, Transform parent, Sprite sprite)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        return image;
    }
}
