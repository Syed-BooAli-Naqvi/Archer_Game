using System;
using System.Collections;
using UnityEngine;

public class ArcherController : MonoBehaviour
{
    public static ArcherController Instance;

    public static bool startGame;
    
    public ArrowType currentArrowType = ArrowType.SimpleArrow;
    public event Action<ArrowType> OnArrowChange;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void ChangeArrowType(ArrowType newType)
    {
        currentArrowType = newType;
        OnArrowChange?.Invoke(newType);
    }
}