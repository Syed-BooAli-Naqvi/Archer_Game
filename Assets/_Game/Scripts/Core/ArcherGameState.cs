using System;

public static class ArcherGameState
{
    public static bool IsGameRunning { get; private set; }

    public static event Action<bool> OnGameStateChanged;

    public static void StartGame()
    {
        if (IsGameRunning)
            return;

        IsGameRunning = true;

        OnGameStateChanged?.Invoke(true);
    }

    public static void StopGame()
    {
        if (!IsGameRunning)
            return;

        IsGameRunning = false;

        OnGameStateChanged?.Invoke(false);
    }

    public static void ToggleGame()
    {
        if (IsGameRunning)
            StopGame();
        else
            StartGame();
    }
}