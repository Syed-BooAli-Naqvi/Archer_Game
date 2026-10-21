using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LoadingScript : SingletonDoNotDestroy<LoadingScript>
{
    [Header("Loading UI")]
    public Slider loadingFill;
    public TMP_Text loadingProgress;

    [Header("Fake Loading")]
    [Tooltip("How long the fake loading progress takes to reach 100%.")]
    public float fakeLoadingDuration = 5f;

    private void Start()
    {
        StartCoroutine(AsynchronousLoad("My Archery Gameplay"));
    }

    // --------------------------------------------------
    // FAKE LOADING
    // --------------------------------------------------

    public IEnumerator AsynchronousLoad(string scene)
    {
        loadingFill.value = 0f;
        Time.timeScale = 1f;

        CanvasGroup cg = GetComponent<CanvasGroup>();

        if (cg != null)
        {
            cg.alpha = 1f;
            cg.blocksRaycasts = true;
        }

        AsyncOperation ao = SceneManager.LoadSceneAsync(scene);
        ao.allowSceneActivation = false;

        float startTime = Time.realtimeSinceStartup;

        while (true)
        {
            float elapsedTime = Time.realtimeSinceStartup - startTime;

            float progress = Mathf.Clamp01(
                elapsedTime / fakeLoadingDuration
            );

            loadingFill.value = progress;

            if (loadingProgress != null)
            {
                int percentage = Mathf.RoundToInt(progress * 100f);
                loadingProgress.text = "Loading..." + percentage + "%";
            }

            if (progress >= 1f)
                break;

            yield return null;
        }

        loadingFill.value = 1f;

        if (loadingProgress != null)
            loadingProgress.text = "Loading...100%";

        yield return new WaitUntil(() => ao.progress >= 0.9f);

        ao.allowSceneActivation = true;

        yield return new WaitForSecondsRealtime(0.5f);

        HideLoading();
    }

    // --------------------------------------------------
    // ASYNC LOADING - NO UI
    // --------------------------------------------------

    public void LoadSceneAsyncNoUI(string scene)
    {
        StartCoroutine(LoadSceneAsyncNoUICoroutine(scene));
    }

    private IEnumerator LoadSceneAsyncNoUICoroutine(string scene)
    {
        Time.timeScale = 1f;

        AsyncOperation ao = SceneManager.LoadSceneAsync(scene);

        // Scene will activate automatically when loading reaches 90%.
        ao.allowSceneActivation = true;

        yield return ao;
    }

    // --------------------------------------------------
    // HIDE LOADING UI
    // --------------------------------------------------

    private void HideLoading()
    {
        if (loadingProgress != null)
            loadingProgress.text = "0 %";

        if (loadingFill != null)
            loadingFill.value = 0f;

        CanvasGroup cg = GetComponent<CanvasGroup>();

        if (cg != null)
        {
            cg.alpha = 0f;
            cg.blocksRaycasts = false;
        }
    }
}