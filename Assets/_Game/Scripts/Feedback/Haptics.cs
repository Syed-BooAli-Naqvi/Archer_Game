using Lofelt.NiceVibrations;
using UnityEngine;

public enum HapticType { Light, Medium, Heavy, Success, Failure }

// Thin wrapper over Nice Vibrations so gameplay code stays decoupled from the package.
public static class Haptics
{
    // Rapid light/medium hits (coins, arrows) are throttled so they don't blur into one buzz.
    // Longer gap on Android, where basic motors get longer pulses (see BasicMotorPattern).
#if UNITY_ANDROID && !UNITY_EDITOR
    private const float MinGap = 0.15f;
#else
    private const float MinGap = 0.08f;
#endif
    private static float lastTime = -10f;

    public static bool Enabled
    {
        get => PlayerPrefs.GetInt(SharedPref.Vibration, 1) == 1;
        set
        {
            PlayerPrefs.SetInt(SharedPref.Vibration, value ? 1 : 0);
            PlayerPrefs.Save();
            HapticController.hapticsEnabled = value;
        }
    }

    // Init on the main thread at startup so the first in-game haptic has no hitch.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        HapticController.hapticsEnabled = Enabled;
        bool advanced = HapticController.Init();

        // One line in logcat/Xcode so a device that doesn't vibrate can be diagnosed from its log.
        Debug.Log($"Haptics: enabled={Enabled} advanced={advanced} versionSupported={DeviceCapabilities.isVersionSupported} " +
                  $"amplitudeControl={DeviceCapabilities.hasAmplitudeControl} os={SystemInfo.operatingSystem}");
    }

    private static bool loggedFirstPlay;

    public static void Play(HapticType type)
    {
        if (!Enabled) return;

        bool important = type == HapticType.Success || type == HapticType.Failure;
        if (!important && Time.unscaledTime - lastTime < MinGap) return;
        lastTime = Time.unscaledTime;

        if (!loggedFirstPlay)
        {
            loggedFirstPlay = true;
            Debug.Log($"Haptics: first play {type} (controllerEnabled={HapticController.hapticsEnabled})");
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        // Phones without amplitude control (most budget Androids, e.g. Galaxy A52) only get Nice Vibrations'
        // on/off fallback, where Light is 40ms: too short for their motors to spin up, so it can't be felt.
        if (!HapticController.Init() && DeviceCapabilities.isVersionSupported)
        {
            LofeltHaptics.PlayMaximumAmplitudePattern(BasicMotorPattern(type));
            return;
        }
#endif

        HapticPatterns.PlayPreset(ToPreset(type));
    }

    // Cumulative times in seconds, alternating on/off starting with on (Nice Vibrations' pattern format).
    private static float[] BasicMotorPattern(HapticType type)
    {
        switch (type)
        {
            case HapticType.Light: return new[] { 0f, 0.07f };
            case HapticType.Medium: return new[] { 0f, 0.12f };
            case HapticType.Heavy: return new[] { 0f, 0.22f };
            case HapticType.Success: return new[] { 0f, 0.09f, 0.17f, 0.40f };
            case HapticType.Failure: return new[] { 0f, 0.25f, 0.35f, 0.60f };
            default: return new[] { 0f, 0.07f };
        }
    }

    private static HapticPatterns.PresetType ToPreset(HapticType type)
    {
        switch (type)
        {
            case HapticType.Light: return HapticPatterns.PresetType.LightImpact;
            case HapticType.Medium: return HapticPatterns.PresetType.MediumImpact;
            case HapticType.Heavy: return HapticPatterns.PresetType.HeavyImpact;
            case HapticType.Success: return HapticPatterns.PresetType.Success;
            case HapticType.Failure: return HapticPatterns.PresetType.Failure;
            default: return HapticPatterns.PresetType.None;
        }
    }
}
