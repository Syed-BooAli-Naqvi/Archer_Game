using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The 2X booster button that lives in Canvas/Gameplay (disabled by default).
/// BoosterPickup enables it while the player has a charge and disables it otherwise.
/// Create it with Tools/Create 2X Booster Button.
/// </summary>
[RequireComponent(typeof(Button))]
public class BoosterButton : MonoBehaviour
{
    [Tooltip("Small 'x2' badge shown when more than one charge is stored.")]
    public TMP_Text chargeBadge;

    private Button button;

    public RectTransform Rect => (RectTransform)transform;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(OnClick);
    }

    private void OnClick()
    {
        if (BoosterPickup.Instance != null)
            BoosterPickup.Instance.OnButtonPressed();
    }

    /// <summary>The scene's button, found under the gameplay UI even while disabled.</summary>
    public static BoosterButton Find()
    {
        MainMenuUIHandler ui = MainMenuUIHandler.Instance;
        if (ui != null && ui.gameplayObj != null)
        {
            BoosterButton found = ui.gameplayObj.GetComponentInChildren<BoosterButton>(true);
            if (found != null)
                return found;
        }

        return FindAnyObjectByType<BoosterButton>(FindObjectsInactive.Include);
    }
}
