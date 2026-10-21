using TMPro;
using UnityEngine;

public class X22 : MonoBehaviour
{
    [System.Serializable]
    public enum X23432
    {
        X2, X3, X4
    }

    public static X23432 x23432;

    public TMP_Text xText;
    public Animator x22Anim;

    private static Animator x22Animator;

    private void Awake()
    {
        x22Animator = x22Anim;
    }

    public static void StopX()
    {
        if (x22Animator != null)
        {
            x22Animator.enabled = false;
        }
    }

    public void SetX(X23432 x23432)
    {
        X22.x23432 = x23432;

        switch (x23432)
        {
            case X23432.X2:
                xText.text = "2";
                break;

            case X23432.X3:
                xText.text = "3";
                break;

            case X23432.X4:
                xText.text = "4";
                break;
        }
    }
}