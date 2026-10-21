using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class SoundButton : MonoBehaviour
{
    void Start()
    {
        GetComponent<Button>().onClick.AddListener(()=>
        {
            if (SoundManager.Instance)
                SoundManager.Instance.PlaySound(SoundName.ButtonClick);
        });
    }
}
