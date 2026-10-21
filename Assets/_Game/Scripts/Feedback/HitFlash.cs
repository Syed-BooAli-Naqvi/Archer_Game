using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Red glow around the screen edges when the player is hit (DifficultySettings.hitFeedback).
/// Built at runtime under Canvas > Gameplay; the edge texture is generated, so no art is needed.
/// </summary>
public class HitFlash : MonoBehaviour
{
    private const float MaxAlpha = 0.7f;
    private const float FadeTime = 0.45f;

    private static HitFlash instance;

    private Image image;
    private float alpha;

    /// <param name="strength">Damage as a share of max health; bigger hits glow brighter.</param>
    public static void Flash(float strength)
    {
        if (instance == null && !Create())
            return;

        float target = Mathf.Lerp(0.35f, MaxAlpha, Mathf.Clamp01(strength * 4f));
        instance.alpha = Mathf.Max(instance.alpha, target);
        instance.image.enabled = true;
    }

    private static bool Create()
    {
        MainMenuUIHandler ui = MainMenuUIHandler.Instance;
        if (ui == null || ui.gameplayObj == null)
            return false;

        GameObject go = new GameObject("Hit Flash", typeof(RectTransform), typeof(Image));
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(ui.gameplayObj.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.SetAsFirstSibling();   // behind the HUD

        instance = go.AddComponent<HitFlash>();
        instance.image = go.GetComponent<Image>();
        instance.image.sprite = EdgeSprite();
        instance.image.raycastTarget = false;
        instance.image.color = new Color(1f, 0f, 0f, 0f);
        instance.image.enabled = false;
        return true;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Update()
    {
        if (!image.enabled)
            return;

        alpha = Mathf.MoveTowards(alpha, 0f, MaxAlpha * Time.unscaledDeltaTime / FadeTime);
        image.color = new Color(0.9f, 0f, 0f, alpha);

        if (alpha <= 0f)
            image.enabled = false;
    }

    // Clear in the middle, red towards the edges.
    private static Sprite EdgeSprite()
    {
        const int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        Color32[] pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Abs(x / (size - 1f) * 2f - 1f);
                float dy = Mathf.Abs(y / (size - 1f) * 2f - 1f);
                // Rounded-square distance so the long sides of a phone screen glow too.
                float d = Mathf.Pow(Mathf.Pow(dx, 4f) + Mathf.Pow(dy, 4f), 0.25f);
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, d));
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
}
