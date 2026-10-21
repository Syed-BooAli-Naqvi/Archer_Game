using UnityEngine;

/// <summary>
/// Phones run at 30 fps unless told otherwise; this sets DifficultySettings.targetFrameRate (60) at startup.
/// </summary>
public static class FrameRate
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Apply()
    {
        DifficultySettings settings = DifficultySettings.Current;
        if (settings == null || settings.targetFrameRate <= 0)
            return;

        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = settings.targetFrameRate;
    }
}
