using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The "damage chip" on a health bar: when health drops, a light strip shows the lost part for a moment,
/// then drains down to the new value (DifficultySettings.hitFeedback). A copy of the bar's fill, drawn behind it.
/// </summary>
public class HealthBarChip : MonoBehaviour
{
    private const float HoldTime = 0.35f;
    private const float DrainSpeed = 0.8f;   // share of the bar per second

    private Slider slider;
    private RectTransform chip;
    private float shown;
    private float holdUntil;
    private float lastValue;

    public static void Attach(Slider slider)
    {
        if (slider == null || slider.fillRect == null || slider.GetComponent<HealthBarChip>() != null)
            return;

        // Only bars that grow left-to-right or right-to-left.
        if (slider.direction != Slider.Direction.LeftToRight && slider.direction != Slider.Direction.RightToLeft)
            return;

        slider.gameObject.AddComponent<HealthBarChip>().Build(slider);
    }

    private void Build(Slider target)
    {
        slider = target;

        RectTransform fill = slider.fillRect;
        GameObject copy = Instantiate(fill.gameObject, fill.parent);
        copy.name = "Damage Chip";
        for (int i = copy.transform.childCount - 1; i >= 0; i--)
            Destroy(copy.transform.GetChild(i).gameObject);

        chip = (RectTransform)copy.transform;
        chip.SetSiblingIndex(fill.GetSiblingIndex());   // just behind the real fill

        if (copy.TryGetComponent(out Image image))
        {
            image.color = new Color(1f, 0.92f, 0.55f);
            image.raycastTarget = false;
        }

        shown = lastValue = slider.normalizedValue;
    }

    private void LateUpdate()
    {
        if (chip == null)
            return;

        float value = slider.normalizedValue;

        // Keep the chip waiting while hits keep coming, then drain.
        if (value < lastValue)
            holdUntil = Time.unscaledTime + HoldTime;
        lastValue = value;

        if (value >= shown)
        {
            shown = value;   // healed (or no change): no chip
        }
        else if (Time.unscaledTime >= holdUntil)
        {
            shown = Mathf.MoveTowards(shown, value, DrainSpeed * Time.unscaledDeltaTime);
        }

        // Same shape as the fill, but reaching to the shown value.
        RectTransform fill = slider.fillRect;
        chip.offsetMin = fill.offsetMin;
        chip.offsetMax = fill.offsetMax;
        if (slider.direction == Slider.Direction.LeftToRight)
        {
            chip.anchorMin = fill.anchorMin;
            chip.anchorMax = new Vector2(shown, fill.anchorMax.y);
        }
        else
        {
            chip.anchorMin = new Vector2(1f - shown, fill.anchorMin.y);
            chip.anchorMax = fill.anchorMax;
        }
    }
}
