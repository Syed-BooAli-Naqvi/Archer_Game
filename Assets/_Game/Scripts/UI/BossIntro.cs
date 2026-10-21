using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// When the boss wave arrives: a "BOSS FIGHT!" banner slams in, the camera shakes, the phone buzzes and the
/// music switches to SoundName.BossMusic (DifficultySettings.bossIntro). Built at runtime under Canvas > Gameplay.
/// </summary>
public class BossIntro : MonoBehaviour
{
    private const float SlamTime = 0.3f;
    private const float HoldTime = 1.4f;
    private const float FadeTime = 0.35f;

    private TextMeshProUGUI title;

    public static void Show()
    {
        if (!DifficultySettings.Current.BossIntroActive)
            return;

        SoundManager.Instance.PlaySound(SoundName.BossAppear);
        SoundManager.Instance.PlayBG(SoundName.BossMusic);
        CameraShake.Shake(0.4f, 0.25f);
        Haptics.Play(HapticType.Heavy);

        MainMenuUIHandler ui = MainMenuUIHandler.Instance;
        if (ui == null || ui.gameplayObj == null || !ui.gameplayObj.activeInHierarchy)
            return;

        GameObject go = new GameObject("Boss Intro", typeof(RectTransform));
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(ui.gameplayObj.transform, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.62f);
        rect.sizeDelta = new Vector2(1800f, 300f);

        go.AddComponent<BossIntro>().Build();
    }

    private void Build()
    {
        title = gameObject.AddComponent<TextMeshProUGUI>();
        if (DifficultySettings.Current.damageNumberFont != null)
            title.font = DifficultySettings.Current.damageNumberFont;
        title.text = "BOSS FIGHT!";
        title.fontSize = 190f;
        title.alignment = TextAlignmentOptions.Center;
        title.enableWordWrapping = false;
        title.raycastTarget = false;
        title.outlineWidth = 0.3f;
        title.outlineColor = new Color32(40, 0, 0, 255);
        title.color = Color.white;
        title.enableVertexGradient = true;
        Color top = new Color(1f, 0.85f, 0.3f), bottom = new Color(0.95f, 0.1f, 0.05f);
        title.colorGradient = new VertexGradient(top, top, bottom, bottom);

        StartCoroutine(Animate());
    }

    private IEnumerator Animate()
    {
        // Slams in from big to normal size.
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / SlamTime)
        {
            float e = 1f - (1f - t) * (1f - t) * (1f - t);
            transform.localScale = Vector3.one * Mathf.Lerp(2.6f, 1f, e);
            title.alpha = Mathf.Clamp01(t * 3f);
            yield return null;
        }
        transform.localScale = Vector3.one;
        title.alpha = 1f;

        // Gentle throb while it holds.
        for (float t = 0f; t < HoldTime; t += Time.unscaledDeltaTime)
        {
            transform.localScale = Vector3.one * (1f + 0.04f * Mathf.Sin(t * 12f));
            yield return null;
        }

        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / FadeTime)
        {
            title.alpha = 1f - t;
            transform.localScale = Vector3.one * (1f + 0.15f * t);
            yield return null;
        }

        Destroy(gameObject);
    }
}
