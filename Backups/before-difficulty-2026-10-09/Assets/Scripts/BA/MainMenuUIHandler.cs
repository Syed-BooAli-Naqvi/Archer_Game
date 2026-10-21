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
    }

    public void ShowInapp()
    {
        // iaps[Random.Range(0, iaps.Length)].SetActive(true);
    }

    private void Update()
    {
        foreach (var item in coinTxt)
            item.text = PlayerPrefs.GetInt(SharedPref.Cash).ToString();
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
        TMP_Text rewardTxt = Instantiate(coinsAdded, coinsAdded.transform.parent);
        SoundManager.Instance.PlaySoundUnique(SoundName.CoinCollect);
        rewardTxt.text = "+ " + rewardAmount.ToString();
        rewardTxt.gameObject.SetActive(true);
        Destroy(rewardTxt.gameObject, 3);
    }

    public void ClaimXReward()
    {
        int reward = rewardAmountThisLevel * GetXRewardMultiplier();

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
        foreach (var item in rewardTxt)
        {
            item.text = (rewardAmountThisLevel * UpgradeManager.Instance.Income).ToString();
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
            item.text = (rewardAmountThisLevel * UpgradeManager.Instance.Income).ToString();
        }
        failedPanel.SetActive(true);
        Time.timeScale = 0;
    }

    public void Continue()
    {
        int currentCash = PlayerPrefs.GetInt(SharedPref.Cash);
        int reward = Mathf.RoundToInt(rewardAmountThisLevel * UpgradeManager.Instance.Income);

        PlayerPrefs.SetInt(SharedPref.Cash, currentCash + reward);
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