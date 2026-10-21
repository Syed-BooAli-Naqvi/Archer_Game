using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SoundData", menuName = "ScriptableObjects/SoundData", order = 1)]
public class SoundData : ScriptableObject
{
    public bool SetName;
    public List<Sounds> sounds;

    private void OnValidate()
    {
        if (SetName)
        {
            for (int i = 0; i < sounds.Count; i++)
            {
                sounds[i].name = sounds[i].soundName.ToString();
            }
        }
    }
}

[System.Serializable]
public enum SoundName
{
    None, Background, ButtonClick, ArrowShoot, HitEffect, CoinCollect, Upgrade, Die, Win
}

[System.Serializable]
public class Sounds
{
    [HideInInspector]
    public string name;
    public SoundName soundName;
    public AudioClip clip;
    
    [Range(0f, 1f)]
    public float volume = 1f; // Individual sound volume (0-1)
    
    public bool loop = false;
}