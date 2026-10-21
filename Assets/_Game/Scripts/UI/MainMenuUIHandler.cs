using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using UnityEngine.SceneManagement;


#if UNITY_EDITOR
using UnityEditor;
#endif

public class MainMenuUIHandler : Singleton<MainMenuUIHandler>
{
    public TMP_Text[] coinTxt;
    public Slider music, sound;
    public Button musicBtn, soundBtn;
    public GameObject settingPanel, notEnoughCashPanel, shopPanel, pausePanel, completePanel, failedPanel, exitPanel;
    public TMP_Text[] rewardTxt;
    public GameObject[] iaps;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (music != null)
            music.onValueChanged.AddListener(delegate { SetMusic(music); });
        if (sound != null)
            sound.onValueChanged.AddListener(delegate { SetSound(sound); });

        if (musicBtn != null)
        {
            musicBtn.onClick.AddListener(() =>
            {
                if (PlayerPrefs.GetFloat(SharedPref.MusicLevel, 1) != 0)
                {
                    SetMusic(0);
                }
                else
                {
                    SetMusic(1);
                }
            });
        }

        if (soundBtn != null)
        {
            soundBtn.onClick.AddListener(() =>
            {
                if (PlayerPrefs.GetFloat(SharedPref.SoundLevel, 1) != 0)
                {
                    SetSound(0);
                }
                else
                {
                    SetSound(1);
                }
            });
        }

        if (music != null)
            music.value = PlayerPrefs.GetFloat(SharedPref.MusicLevel, 1);
        if (sound != null)
            sound.value = PlayerPrefs.GetFloat(SharedPref.SoundLevel, 1);
        ShowInapp();
        VibrationSettingRow.Build(music);
        SetupPanelAnimations();
    }

    // Runtime-only extras (DifficultySettings "Panels & menus"); nothing is changed in the scene.
    private void SetupPanelAnimations()
    {
        DifficultySettings settings = DifficultySettings.Current;
        if (settings == null)
            return;

        if (settings.PanelAnimationsActive)
        {
            foreach (GameObject panel in new[] { settingPanel, notEnoughCashPanel, shopPanel, pausePanel, completePanel, failedPanel, exitPanel, gunSelection })
                PanelAnimator.Attach(panel);

            MenuIntroAnimator.Attach(mainMenuObj, FindStartButton());
        }

        if (mainMenuObj != null && mainMenuObj.activeInHierarchy)
            WelcomePanel.TryShow();
    }

    private Transform FindStartButton()
    {
        if (mainMenuObj == null)
            return null;

        foreach (Button button in mainMenuObj.GetComponentsInChildren<Button>(true))
        {
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
            {
                if (button.onClick.GetPersistentMethodName(i) == nameof(TapToStart))
                    return button.transform;
            }
        }

        return null;
    }

    public void ShowInapp()
    {
        // iaps[Random.Range(0, iaps.Length)].SetActive(true);
    }

    private void Update()
    {
        UpdateCoinCounter();
    }

    // ---------- Coin counter: text only changes when the number does; counts up and pops when coins are added ----------
    private int coinTarget = int.MinValue;
    private float coinShown;
    private int coinShownInt = int.MinValue;
    private float coinCountSpeed;
    private float coinPop = 1f;
    private Vector3[] coinBaseScales;

    private const float CoinCountTime = 0.5f;
    private const float CoinPopTime = 0.25f;

    private void UpdateCoinCounter()
    {
        int cash = PlayerPrefs.GetInt(SharedPref.Cash);

        if (cash != coinTarget)
        {
            DifficultySettings settings = DifficultySettings.Current;
            bool animate = settings != null && settings.CoinCounterAnimationActive && coinTarget != int.MinValue && cash > coinTarget;

            CacheCoinBaseScales();
            coinTarget = cash;

            if (animate)
            {
                coinCountSpeed = Mathf.Max(1f, (cash - coinShown) / CoinCountTime);
                if (coinPop >= 1f)
                    coinPop = 0f;
            }
            else
            {
                coinShown = cash;   // first frame, spending or animation off: show it straight away
            }
        }

        if (coinShown < coinTarget)
            coinShown = Mathf.Min(coinTarget, coinShown + coinCountSpeed * Time.unscaledDeltaTime);

        int shown = Mathf.RoundToInt(coinShown);
        if (shown != coinShownInt)
        {
            coinShownInt = shown;
            string text = shown.ToString();
            foreach (var item in coinTxt)
                if (item != null)
                    item.text = text;
        }

        if (coinPop < 1f)
        {
            coinPop = Mathf.Min(1f, coinPop + Time.unscaledDeltaTime / CoinPopTime);
            float scale = 1f + 0.2f * Mathf.Sin(coinPop * Mathf.PI);
            CacheCoinBaseScales();
            for (int i = 0; i < coinTxt.Length; i++)
                if (coinTxt[i] != null)
                    coinTxt[i].transform.localScale = coinBaseScales[i] * scale;
        }
    }

    // Re-cached if coinTxt changes size (e.g. edited in the Inspector during play), so the pop never indexes past the end.
    private void CacheCoinBaseScales()
    {
        if (coinBaseScales != null && coinBaseScales.Length == coinTxt.Length)
            return;

        Vector3[] old = coinBaseScales;
        coinBaseScales = new Vector3[coinTxt.Length];
        for (int i = 0; i < coinTxt.Length; i++)
        {
            if (old != null && i < old.Length)
                coinBaseScales[i] = old[i];
            else if (coinTxt[i] != null)
                coinBaseScales[i] = coinTxt[i].transform.localScale;
        }
    }

    public void OpenUrl(string url)
    {
        Application.OpenURL(url);
    }

    public void OpenSetting()
    {
        settingPanel.SetActive(true);
    }

    public void CloseSetting()
    {
        settingPanel.SetActive(false);
    }

    public void OpenPause()
    {
        pausePanel.SetActive(true);
        Time.timeScale = 0;
    }

    public void ClosePause()
    {
        pausePanel.SetActive(false);
        Time.timeScale = 1;
    }

    public TMP_Text coinsAdded;

    public int rewardAmountThisLevel;
    public void AddToReward(int amount)
    {
        int rewardAmount = Mathf.RoundToInt(amount * UpgradeManager.Instance.Income);
        rewardAmountThisLevel += rewardAmount;

        int currentCash = PlayerPrefs.GetInt(SharedPref.Cash);
        PlayerPrefs.SetInt(SharedPref.Cash, currentCash + rewardAmount);

        // Kills close together add to the same "+X" popup instead of stacking unreadable copies
        // (and the coin sound plays once per popup, not once per kill).
        if (lastCoinPopup != null && Time.unscaledTime - lastCoinPopupTime < 0.5f)
        {
            lastCoinPopupAmount += rewardAmount;
            lastCoinPopup.text = "+ " + lastCoinPopupAmount.ToString();
            return;
        }

        SoundManager.Instance.PlaySoundUnique(SoundName.CoinCollect);

        TMP_Text rewardTxt = Instantiate(coinsAdded, coinsAdded.transform.parent);
        // Draw behind the coin counter so the popup slides under it instead of covering the number.
        rewardTxt.transform.SetSiblingIndex(0);
        rewardTxt.text = "+ " + rewardAmount.ToString();
        rewardTxt.gameObject.SetActive(true);
        Destroy(rewardTxt.gameObject, 3);

        lastCoinPopup = rewardTxt;
        lastCoinPopupAmount = rewardAmount;
        lastCoinPopupTime = Time.unscaledTime;
    }

    private TMP_Text lastCoinPopup;
    private int lastCoinPopupAmount;
    private float lastCoinPopupTime;

    public void ClaimXReward()
    {
        // Coins were already credited as enemies died, so "xN" only adds the remaining (N - 1) shares.
        int reward = rewardAmountThisLevel * (GetXRewardMultiplier() - 1);

        int currentCash = PlayerPrefs.GetInt(SharedPref.Cash);

        PlayerPrefs.SetInt(SharedPref.Cash, currentCash + reward);
        
        Claim();
    }

    // Returns the numeric multiplier for the currently selected X reward
    public int GetXRewardMultiplier()
    {
        switch (X22.x23432)
        {
            case X22.X23432.X3:
                return 3;
            case X22.X23432.X4:
                return 4;
            default:
                return 2;
        }
    }
    
    public void OpenCompletePanel()
    {
        // rewardAmountThisLevel already includes the income multiplier.
        foreach (var item in rewardTxt)
        {
            item.text = rewardAmountThisLevel.ToString();
        }
        completePanel.SetActive(true);
        Time.timeScale = 0;
    }

    public void Claim()
    {
        rewardAmountThisLevel = 0;

        OpenSceneNoUI(SceneManager.GetActiveScene().name);
        ArcherGameState.StopGame();
    }

    public void CloseCompletePanel()
    {
        completePanel.SetActive(false);
        Time.timeScale = 1;
    }

    public void OpenFailedPanel()
    {
        foreach (var item in rewardTxt)
        {
            item.text = rewardAmountThisLevel.ToString();
        }
        failedPanel.SetActive(true);
        Time.timeScale = 0;
    }

    public void Continue()
    {
        // Coins were already credited as enemies died; don't pay them a second time.
        rewardAmountThisLevel = 0;
        
        OpenSceneNoUI(SceneManager.GetActiveScene().name);
        ArcherGameState.StopGame();
    }

    public void CloseFailedPanel()
    {
        failedPanel.SetActive(false);
        Time.timeScale = 1;
    }

    public void Restart()
    {
        OpenSceneNoUI(SceneManager.GetActiveScene().name);
        ArcherGameState.StopGame();
    }

    public void GiveUp()
    {
        OpenSceneNoUI(SceneManager.GetActiveScene().name);
        ArcherGameState.StopGame();
    }

    public void RewardPlayer(int cashAmount)
    {
        PlayerPrefs.SetInt(SharedPref.Cash, PlayerPrefs.GetInt(SharedPref.Cash) + cashAmount);
    }

    public void SetMusic(Slider slider)
    {
        SoundManager.Instance.SetMusicLevel(slider.value);
    }

    public void SetSound(Slider slider)
    {
        SoundManager.Instance.SetSoundLevel(slider.value);
    }

    public void SetMusic(float value)
    {
        SoundManager.Instance.SetMusicLevel(value);
    }

    public void SetSound(float value)
    {
        SoundManager.Instance.SetSoundLevel(value);
    }

    public void OpenScene(string name)
    {
        StartCoroutine(LoadingScript.Instance.AsynchronousLoad(name));
    }

    public void OpenSceneNoUI(string name)
    {
        LoadingScript.Instance.LoadSceneAsyncNoUI(name);
    }

    public void ShowPlayBtnAd()
    {
    }

    public void OpenShop()
    {
        shopPanel.SetActive(true);
    }

    public void CloseShop()
    {
        shopPanel.SetActive(false);
    }

    public void OpenExit()
    {
        exitPanel.SetActive(true);
    }

    public void QuitApplication()
    {
        Application.Quit();
    }

    public void ExitPlayMode()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#endif
    }

    public GameObject gunSelection;

    public void OpenGunSelection()
    {
        gunSelection.SetActive(true);
    }

    public void CloseGunSelection()
    {
        gunSelection.SetActive(false);
    }

    public void CloseExit()
    {
        exitPanel.SetActive(false);
    }

    public void Restore()
    {
    }

    public void VideoAdForCoins()
    {
    }
    public GameObject mainMenuObj, gameplayObj;
    public GameObject mainMenuCam, gameplayTopCam;
    public Image blackFade;
    public TMP_Text countdownText;

    public float fadeDuration = 0.5f;

    public void TapToStart()
    {
        rewardAmountThisLevel = 0;
        StartCoroutine(StartGame());
    }

    private IEnumerator StartGame()
    {
        // Fade 0 -> 1
        // yield return Fade(0f, 1f);

        // // Screen is completely black
        mainMenuObj.SetActive(false);
        // yield return new WaitForSeconds(1);

        // // Fade 1 -> 0
        // yield return Fade(1f, 0f);

        // Enable gameplay camera first
        gameplayTopCam.SetActive(true);

        yield return new WaitForSeconds(1);

        // Disable main menu camera
        mainMenuCam.SetActive(false);

        // blackFade.gameObject.SetActive(false);

        gameplayObj.SetActive(true);

        // Countdown
        yield return StartCoroutine(Countdown());

        // Start actual game
        ArcherGameState.StartGame();
    }

    private IEnumerator Countdown()
    {
        // countdownText.gameObject.SetActive(true);

        // countdownText.text = "3";
        // yield return new WaitForSeconds(0.2f);

        // countdownText.text = "2";
        // yield return new WaitForSeconds(0.2f);

        // countdownText.text = "1";
        // yield return new WaitForSeconds(0.2f);

        countdownText.text = "GO!";
        DifficultySettings settings = DifficultySettings.Current;
        if (settings != null && settings.PanelAnimationsActive)
            StartCoroutine(PanelAnimator.Punch(countdownText.transform));
        yield return new WaitForSeconds(1.5f);

        countdownText.gameObject.SetActive(false);
    }

    private IEnumerator Fade(float from, float to)
    {
        blackFade.gameObject.SetActive(true);

        float time = 0f;
        Color color = blackFade.color;

        while (time < fadeDuration)
        {
            time += Time.deltaTime;

            float alpha = Mathf.Lerp(from, to, time / fadeDuration);
            color.a = alpha;
            blackFade.color = color;

            yield return null;
        }

        color.a = to;
        blackFade.color = color;
    }
}