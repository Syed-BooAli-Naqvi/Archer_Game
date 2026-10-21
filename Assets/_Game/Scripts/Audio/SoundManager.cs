using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance;
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(this.gameObject);
        }
        else
            DestroyImmediate(this.gameObject);
    }
    public SoundData data;
    public AudioSource BG;
    private List<AudioSource> audioSources = new List<AudioSource>();

    public bool test;

    // Track currently playing unique sounds
    private Dictionary<SoundName, AudioSource> activeSounds = new Dictionary<SoundName, AudioSource>();

    private void OnValidate()
    {
        if (test)
        {
            test = false;
            PlaySound(SoundName.ButtonClick);
        }
    }

    private void Start()
    {
        PlayBG(SoundName.Background);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // A new run starts with the normal music again (the boss fight switches to SoundName.BossMusic).
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Sounds bg = data.sounds.Find(s => s.soundName == SoundName.Background);
        if (bg != null && BG.clip != bg.clip)
            PlayBG(SoundName.Background);
    }

    // ---------------- BACKGROUND ----------------

    public void PlayBG(SoundName name)
    {
        foreach (var sound in data.sounds)
        {
            if (sound.soundName == name)
            {
                BG.loop = sound.loop;
                BG.playOnAwake = false;
                BG.clip = sound.clip;

                float soundVolume = sound.volume;
                float musicLevel = PlayerPrefs.GetFloat(SharedPref.MusicLevel, 1);
                BG.volume = Mathf.Clamp01(soundVolume * musicLevel);

                BG.Play();
                return;
            }
        }
    }

    // ---------------- NORMAL PLAY ----------------

    public void PlaySound(SoundName name = SoundName.None, GameObject targetObj = null)
    {
        bool destroyAfter = false;

        if (!targetObj)
        {
            destroyAfter = true;
            targetObj = new GameObject(name.ToString());
            targetObj.transform.SetParent(transform);
        }

        AudioSource audioSource = targetObj.GetComponent<AudioSource>();
        if (!audioSource)
            audioSource = targetObj.AddComponent<AudioSource>();

        foreach (var sound in data.sounds)
        {
            if (sound.soundName == name && sound.clip != null)
            {
                audioSource.loop = sound.loop;
                audioSource.playOnAwake = false;
                audioSource.clip = sound.clip;

                float soundVolume = sound.volume;
                float soundLevel = PlayerPrefs.GetFloat(SharedPref.SoundLevel, 1);
                audioSource.volume = Mathf.Clamp01(soundVolume * soundLevel);

                audioSource.Play();

                if (destroyAfter && !audioSource.loop)
                    StartCoroutine(DestroyAfterTime(targetObj, audioSource.clip.length));

                return;
            }
        }

        // No clip set up for this sound.
        if (destroyAfter)
            Destroy(targetObj);
    }

    // ---------------- UNIQUE PLAY ----------------
    // Will NOT play if same sound is already playing

    public void PlaySoundUnique(SoundName name = SoundName.None, GameObject targetObj = null)
    {
        if (activeSounds.ContainsKey(name))
        {
            AudioSource existing = activeSounds[name];
            if (existing && existing.isPlaying)
                return;

            activeSounds.Remove(name);
        }

        bool destroyAfter = false;

        if (!targetObj)
        {
            destroyAfter = true;
            targetObj = new GameObject(name.ToString());
            targetObj.transform.SetParent(transform);
        }

        AudioSource audioSource = targetObj.GetComponent<AudioSource>();
        if (!audioSource)
            audioSource = targetObj.AddComponent<AudioSource>();

        foreach (var sound in data.sounds)
        {
            if (sound.soundName == name && sound.clip != null)
            {
                audioSource.loop = sound.loop;
                audioSource.playOnAwake = false;
                audioSource.clip = sound.clip;

                float soundVolume = sound.volume;
                float soundLevel = PlayerPrefs.GetFloat(SharedPref.SoundLevel, 1);
                audioSource.volume = Mathf.Clamp01(soundVolume * soundLevel);

                audioSource.Play();

                activeSounds[name] = audioSource;

                if (!audioSource.loop)
                    StartCoroutine(RemoveUniqueAfterPlay(name, targetObj, audioSource.clip.length, destroyAfter));

                return;
            }
        }

        if (destroyAfter)
            Destroy(targetObj);
    }

    IEnumerator RemoveUniqueAfterPlay(SoundName name, GameObject obj, float time, bool destroy)
    {
        yield return new WaitForSeconds(time);

        if (activeSounds.ContainsKey(name))
            activeSounds.Remove(name);

        if (destroy && obj)
            Destroy(obj);
    }

    IEnumerator DestroyAfterTime(GameObject obj, float time)
    {
        yield return new WaitForSeconds(time);
        if (obj)
            Destroy(obj);
    }

    public void StopUniqueSound(SoundName name)
    {
        if (activeSounds.ContainsKey(name))
        {
            AudioSource src = activeSounds[name];

            if (src)
            {
                src.Stop();
                Destroy(src.gameObject);
            }

            activeSounds.Remove(name);
        }
    }

    // ---------------- SETTINGS ----------------

    public void SetMusicLevel(float level)
    {
        PlayerPrefs.SetFloat(SharedPref.MusicLevel, level);

        Sounds bgSound = data.sounds.Find(s => s.soundName == SoundName.Background);

        if (bgSound != null)
            BG.volume = Mathf.Clamp01(bgSound.volume * level);
        else
            BG.volume = Mathf.Clamp01(level);
    }

    public void SetSoundLevel(float level)
    {
        PlayerPrefs.SetFloat(SharedPref.SoundLevel, level);
        ApplyVolume();
    }

    public void UpdateAllSoundVolumes()
    {
        SetMusicLevel(PlayerPrefs.GetFloat(SharedPref.MusicLevel, 1));

        AudioSource[] sources = GetComponentsInChildren<AudioSource>();

        foreach (AudioSource source in sources)
        {
            if (source == BG) continue;

            float soundLevel = PlayerPrefs.GetFloat(SharedPref.SoundLevel, 1);
            source.volume = Mathf.Clamp01(source.volume * soundLevel);
        }
    }

    private Dictionary<AudioSource, float> audioBaseVolumes = new Dictionary<AudioSource, float>();

    public void Register(AudioSource source)
    {
        if (source == null) return;

        if (!audioBaseVolumes.ContainsKey(source))
        {
            // store the original volume at registration time
            audioBaseVolumes[source] = source.volume;
        }

        ApplyVolume(source);
    }

    public void Unregister(AudioSource source)
    {
        if (source == null) return;
        if (audioBaseVolumes.ContainsKey(source))
            audioBaseVolumes.Remove(source);
    }

    public void ApplyVolume()
    {
        float globalVolume = PlayerPrefs.GetFloat(SharedPref.SoundLevel, 1);

        foreach (var kvp in audioBaseVolumes)
        {
            AudioSource src = kvp.Key;
            if (src != null)
                src.volume = kvp.Value * globalVolume;
        }
    }

    private void ApplyVolume(AudioSource src)
    {
        if (src == null) return;
        if (audioBaseVolumes.TryGetValue(src, out float baseVol))
        {
            float globalVolume = PlayerPrefs.GetFloat(SharedPref.SoundLevel, 1);
            src.volume = baseVol * globalVolume;
        }
    }

}