using UnityEngine;
using System;

public class RuntimeGameConfig : MonoBehaviour
{
    public static RuntimeGameConfig Instance { get; private set; }
    public int selectedMicrophoneIndex = 0; // Referenced by MicrophoneManager
    public bool speechRecognitionAvailable = true; // Set in MinigameMenuController
    public bool systemChecksPerformed = false; // Set in MinigameMenuController

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
}
