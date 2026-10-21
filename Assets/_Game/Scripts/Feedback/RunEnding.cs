using System.Collections;
using UnityEngine;

/// <summary>
/// How a run ends. Player death: the fail panel opens straight away. Boss death: explosion, camera shake and
/// slow motion, then the win panel (DifficultySettings.bossDeathFinale). Only the first ending counts, so the
/// player can't die after the boss is beaten (or win after dying).
/// </summary>
public class RunEnding : MonoBehaviour
{
    private const float SlowScale = 0.3f;
    private const float SlowInTime = 0.15f;

    private static RunEnding instance;

    private bool ended;
    private bool won;
    private float baseFixedDeltaTime;

    /// <summary>True once the run is won or lost.</summary>
    public static bool Ended => instance != null && instance.ended;

    /// <summary>True once the boss is beaten (the player can't be hurt any more).</summary>
    public static bool Won => instance != null && instance.won;

    public static void Lose()
    {
        if (!Begin(false))
            return;

        MainMenuUIHandler.Instance.OpenFailedPanel();
    }

    public static void Win(Vector3 bossPosition)
    {
        if (!Begin(true))
            return;

        DifficultySettings settings = DifficultySettings.Current;
        if (!settings.BossDeathFinaleActive)
        {
            MainMenuUIHandler.Instance.OpenCompletePanel();
            return;
        }

        if (settings.bossDeathFx != null)
        {
            GameObject fx = Instantiate(settings.bossDeathFx, bossPosition + Vector3.up * 1.5f, Quaternion.identity);
            fx.transform.localScale *= 2f;
            Destroy(fx, 5f);
        }

        CameraShake.Shake(0.6f, 0.35f);

        instance.StartCoroutine(instance.SlowThenWin(settings.winPanelDelay));
    }

    private static bool Begin(bool win)
    {
        if (instance == null)
        {
            instance = new GameObject("Run Ending").AddComponent<RunEnding>();
            instance.baseFixedDeltaTime = Time.fixedDeltaTime;
        }

        if (instance.ended)
            return false;

        instance.ended = true;
        instance.won = win;
        return true;
    }

    private IEnumerator SlowThenWin(float delay)
    {
        float elapsed = 0f;

        while (elapsed < delay)
        {
            // Pause panel open: wait, don't fight it over the time scale.
            if (Time.timeScale > 0f)
            {
                elapsed += Time.unscaledDeltaTime;
                SetTimeScale(Mathf.Lerp(1f, SlowScale, elapsed / SlowInTime));
            }

            yield return null;
        }

        SetTimeScale(1f);
        MainMenuUIHandler.Instance.OpenCompletePanel();
    }

    private void SetTimeScale(float scale)
    {
        Time.timeScale = scale;
        // Physics steps shrink with time so slow motion stays smooth.
        Time.fixedDeltaTime = baseFixedDeltaTime * scale;
    }

    private void OnDestroy()
    {
        if (instance != this)
            return;

        instance = null;
        if (baseFixedDeltaTime > 0f)
            Time.fixedDeltaTime = baseFixedDeltaTime;
    }
}
