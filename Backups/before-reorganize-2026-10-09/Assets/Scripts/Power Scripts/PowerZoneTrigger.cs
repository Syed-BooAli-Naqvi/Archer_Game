using System.Collections.Generic;
using UnityEngine;

public class PowerZoneTrigger : MonoBehaviour
{
    public enum Zone
    {
        Left,
        Right
    }

    [System.Serializable]
    public struct ZoneSprites
    {
        public ArrowType zone;
        public Sprite on, off;
    }

    [Header("Zone")]
    [Tooltip("Which side this trigger belongs to.")]
    public Zone zone;

    [Tooltip("Ability for player when it passes a zone.")]
    public ArrowType zoneAbility;
    public bool removeAbility;

    public List<ZoneSprites> zoneSprites = new();
    public SpriteRenderer zoneAblitySprite;

    [Header("References")]
    public PowerScaler powerScaler;
    public Transform player;

    [Tooltip("Trigger will not work when player is closer than this distance.")]
    public float minimumPlayerDistance = 5f;

    private void OnValidate()
    {
        if (zoneAblitySprite == null)
            return;

        for (int i = 0; i < zoneSprites.Count; i++)
        {
            if (zoneSprites[i].zone == zoneAbility)
            {
                zoneAblitySprite.sprite = removeAbility ? zoneSprites[i].off : zoneSprites[i].on;
                return;
            }
        }

        // No matching sprite found
        zoneAblitySprite.sprite = null;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (powerScaler == null)
            return;

        ArrowController arrow = other.GetComponent<ArrowController>();
        if (arrow != null)
            TriggeredArrow(arrow);

        ArcherArrowController archerArrowController = other.GetComponent<ArcherArrowController>();
        if (archerArrowController != null)
            TriggeredPlayer();
    }

    private static float lastShakeTime = -10f;

    public void TriggeredArrow(ArrowController arrow)
    {
        if (powerScaler.power == 0 || powerScaler.power == 1)
            return;

        if (player == null)
            return;

        float distance = Vector3.Distance(transform.position, player.position);

        if (distance < minimumPlayerDistance)
            return;

        if (arrow.collidedWithRB)
            return;

        arrow.collidedWithRB = true;

        // Small camera shake for each arrow that hits a gate (throttled so a stream of arrows is a rumble).
        DifficultySettings settings = DifficultySettings.Current;
        if (settings.CameraShakeActive && Time.time - lastShakeTime >= settings.gateShakeCooldown)
        {
            lastShakeTime = Time.time;
            CameraShake.Shake(settings.gateShakeDuration, settings.gateShakeStrength);
        }

        if (arrow.arrowType != ArrowType.ReversedArrow)
        {
            // With arrow fading on, the arrow passes through the gate and fades out instead of vanishing.
            if (settings.ArrowFadeActive)
                arrow.BeginFade(0.5f, true);
            else
                arrow.ReturnToPool();
        }
        else if (arrow.arrowType == ArrowType.ReversedArrow)
        {
            arrow.ReverseDirection(transform.forward);
        }

        switch (zone)
        {
            case Zone.Left:
                powerScaler.IncreaseRed();
                break;

            case Zone.Right:
                powerScaler.IncreaseBlue();
                break;
        }
    }

    public void TriggeredPlayer()
    {
        if (powerScaler.usedForAbility)
            return;

        powerScaler.usedForAbility = true;
        
        if (removeAbility)
        {
            if (zoneAbility == ArcherController.Instance.currentArrowType)
            {
                ArcherController.Instance.ChangeArrowType(ArrowType.SimpleArrow);
            }
            return;
        }
        ArcherController.Instance.ChangeArrowType(zoneAbility);
    }
}