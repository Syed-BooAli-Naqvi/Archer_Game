using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Welcome, Archer!" popup with a quick how-to-play (DifficultySettings.welcomePanel). The panel is placed in the
/// gameplay scene (Tools > Archery > Place Welcome Panel In Scene) and kept inactive; this only shows and hides it.
/// Shown on the very first launch, or once per app launch if welcomeEveryLaunch is on.
/// </summary>
public class WelcomePanel : MonoBehaviour
{
    private const string SeenKey = "WelcomePanelSeen";
    private const string GiftKey = "WelcomeCoinsGiven";
    private static bool shownThisLaunch;

    [SerializeField] private CanvasGroup group;
    [SerializeField] private RectTransform window;
    [SerializeField] private Button button;
    [Tooltip("Shows the starter gift; hidden when there is no gift.")]
    [SerializeField] private TMP_Text giftText;
    [SerializeField] private TMP_Text buttonLabel;

    private int giftCoins;
    private Vector3 buttonScale = Vector3.one;

    public static void TryShow()
    {
        DifficultySettings settings = DifficultySettings.Current;
        if (settings == null || !settings.WelcomePanelActive || shownThisLaunch)
            return;

        if (!settings.welcomeEveryLaunch && PlayerPrefs.GetInt(SeenKey, 0) == 1)
            return;

        WelcomePanel panel = FindFirstObjectByType<WelcomePanel>(FindObjectsInactive.Include);
        if (panel == null)
        {
            Debug.LogWarning("WelcomePanel: no Welcome Panel in the scene. Use Tools > Archery > Place Welcome Panel In Scene.");
            return;
        }

        shownThisLaunch = true;
        PlayerPrefs.SetInt(SeenKey, 1);
        PlayerPrefs.Save();

        panel.Open(settings);
    }

    /// <summary>Lets the welcome panel show again (for testing).</summary>
    public static void ResetSeen()
    {
        PlayerPrefs.DeleteKey(SeenKey);
        PlayerPrefs.DeleteKey(GiftKey);
        shownThisLaunch = false;
    }

    /// <summary>Called by the editor tool after it builds the panel.</summary>
    public void Wire(CanvasGroup group, RectTransform window, Button button, TMP_Text giftText, TMP_Text buttonLabel)
    {
        this.group = group;
        this.window = window;
        this.button = button;
        this.giftText = giftText;
        this.buttonLabel = buttonLabel;
    }

    private void Open(DifficultySettings settings)
    {
        gameObject.SetActive(true);

        giftCoins = PlayerPrefs.GetInt(GiftKey, 0) == 1 ? 0 : Mathf.Max(0, settings.welcomeCoins);

        if (giftText != null)
        {
            giftText.gameObject.SetActive(giftCoins > 0);
            giftText.text = "Starter gift: <color=#ffcf40>" + giftCoins + " COINS</color>";
        }

        if (buttonLabel != null)
            buttonLabel.text = giftCoins > 0 ? "COLLECT" : "LET'S GO!";

        if (button != null)
        {
            buttonScale = button.transform.localScale;
            button.onClick.RemoveListener(Close);
            button.onClick.AddListener(Close);
        }

        if (group != null)
            group.interactable = true;

        StartCoroutine(OpenRoutine());
    }

    private IEnumerator OpenRoutine()
    {
        Transform buttonTransform = button != null ? button.transform : null;
        float t = 0f;

        while (t < 0.6f)
        {
            t += Time.unscaledDeltaTime;
            if (group != null)
                group.alpha = Mathf.Clamp01(t / 0.2f);
            if (window != null)
                window.localScale = Vector3.one * Mathf.LerpUnclamped(0.7f, 1f, PanelAnimator.EaseOutBack(Mathf.Clamp01(t / 0.4f)));
            if (buttonTransform != null)
                buttonTransform.localScale = buttonScale * PanelAnimator.EaseOutBack(Mathf.Clamp01((t - 0.3f) / 0.3f));
            yield return null;
        }

        if (group != null)
            group.alpha = 1f;
        if (window != null)
            window.localScale = Vector3.one;

        // Gentle pulse on the button until it's pressed
        while (buttonTransform != null)
        {
            buttonTransform.localScale = buttonScale * (1f + 0.05f * Mathf.Sin(Time.unscaledTime * 4f));
            yield return null;
        }
    }

    private void Close()
    {
        StopAllCoroutines();
        if (group != null)
            group.interactable = false;
        if (button != null)
            button.transform.localScale = buttonScale;
        GiveGift();
        StartCoroutine(CloseRoutine());
    }

    private IEnumerator CloseRoutine()
    {
        float t = 0f;

        while (t < 0.2f)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / 0.2f);
            if (group != null)
                group.alpha = 1f - k;
            if (window != null)
                window.localScale = Vector3.one * Mathf.Lerp(1f, 0.85f, k);
            yield return null;
        }

        if (window != null)
            window.localScale = Vector3.one;
        gameObject.SetActive(false);
    }

    private void GiveGift()
    {
        if (giftCoins <= 0 || PlayerPrefs.GetInt(GiftKey, 0) == 1)
            return;

        PlayerPrefs.SetInt(GiftKey, 1);

        // UpgradeManager keeps its own copy of the cash, so go through it when it exists.
        if (UpgradeManager.Instance != null)
            UpgradeManager.Instance.AddCash(giftCoins);
        else
            PlayerPrefs.SetInt(SharedPref.Cash, PlayerPrefs.GetInt(SharedPref.Cash, 0) + giftCoins);

        PlayerPrefs.Save();

        if (SoundManager.Instance != null)
            SoundManager.Instance.PlaySound(SoundName.Upgrade);

        // Coins fly from the button up into the coin counter
        Camera cam = Camera.main;
        if (cam != null && button != null)
        {
            Vector3 screen = RectTransformUtility.WorldToScreenPoint(null, button.transform.position);
            CoinFlyFX.Fly(cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 10f)), 10);
        }
    }
}
