using UnityEngine;

public class MicrophoneManagerSingleton : MonoBehaviour
{
    public static MicrophoneManagerSingleton Instance { get; private set; }
    
    private MicrophoneManager microphoneManager;
    
    void Awake()
    {
        Debug.Log("[Mic Singleton] Waking up...");
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Initialize microphone manager
            microphoneManager = gameObject.AddComponent<MicrophoneManager>();
            //microphoneManager.Start();
            Debug.Log("[Mic Singleton] Started MicrophoneManager instance.");
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    public MicrophoneManager GetMicrophoneManager()
    {
        return microphoneManager;
    }
}
