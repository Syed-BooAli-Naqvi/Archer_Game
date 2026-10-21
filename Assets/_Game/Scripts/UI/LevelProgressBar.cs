using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives Canvas > Gameplay > Progress Bar: fills from 0% to 100% as the run approaches the boss,
/// then shows "BOSS!" while the boss fight is on. Created by EnemySpawner; finds the bar by name.
/// </summary>
public class LevelProgressBar : MonoBehaviour
{
    public static LevelProgressBar Instance { get; private set; }

    private Slider slider;
    private TMP_Text label;
    private float shown;
    private int lastPercent = -1;
    private bool bossShown;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        // The bar lives under Canvas > Gameplay, which is inactive until the run starts.
        foreach (Slider s in FindObjectsByType<Slider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (s.name == "Progress Bar")
            {
                slider = s;
                break;
            }
        }

        if (slider == null)
        {
            Debug.LogWarning("LevelProgressBar: no 'Progress Bar' slider found under the Canvas.");
            enabled = false;
            return;
        }

        slider.interactable = false;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 0f;
        label = slider.GetComponentInChildren<TMP_Text>(true);
        SetLabel("0%");
    }

    private void Update()
    {
        if (slider == null)
            return;

        float target = EnemySpawner.LevelProgress;
        shown = Mathf.MoveTowards(shown, target, Time.deltaTime * 0.5f);   // smooth, never jumps
        slider.value = shown;

        if (EnemySpawner.BossTriggered)
        {
            if (!bossShown)
            {
                bossShown = true;
                SetLabel("BOSS!");
            }
            return;
        }

        int percent = Mathf.FloorToInt(shown * 100f);
        if (percent != lastPercent)
        {
            lastPercent = percent;
            SetLabel(percent + "%");
        }
    }

    private void SetLabel(string text)
    {
        if (label != null)
            label.text = text;
    }

    /// <summary>For playtests: where the bar is and whether it is actually visible.</summary>
    public string Describe()
    {
        if (slider == null)
            return "not found";

        RectTransform rect = (RectTransform)slider.transform;
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        Image background = slider.GetComponent<Image>();
        Canvas canvas = slider.GetComponentInParent<Canvas>();
        return $"active={slider.gameObject.activeInHierarchy} value={slider.value:0.00} label='{(label != null ? label.text : "-")}' " +
               $"screen x {corners[0].x:0}-{corners[2].x:0} y {corners[0].y:0}-{corners[2].y:0} (screen {Screen.width}x{Screen.height}) " +
               $"bg alpha={(background != null ? background.color.a : -1):0.00} canvas={(canvas != null ? canvas.name + " " + canvas.renderMode : "none")}";
    }
}
