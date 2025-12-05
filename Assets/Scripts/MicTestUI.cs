using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MicTestUI : MonoBehaviour
{
    [Header("UI")]
    public GameObject panelRoot;      
    public Slider levelSlider;      
    public TMP_Text statusText;       

    [Header("Tuning")]
    public float levelGain = 6f;      
    public float smoothSpeed = 10f;  
    public float whisperTimeout = 5f; 

    private MicrophoneManager mic;
    private float smoothedLevel = 0f;

    private void Start()
    {
        mic = MicrophoneManagerSingleton.Instance?.GetMicrophoneManager();
    }

    private void Update()
    {
        if (mic == null)
        {
            if (statusText != null)
                statusText.text = "No MicrophoneManager / speech pipeline available.";
            return;
        }

        float target = Mathf.Clamp01(mic.currentLevel * levelGain);
        smoothedLevel = Mathf.Lerp(smoothedLevel, target, Time.deltaTime * smoothSpeed);

        if (levelSlider != null)
            levelSlider.value = smoothedLevel;

        if (statusText != null)
        {
            if (!mic.IsStreaming)
            {
                statusText.text =
                    "Starting FFmpeg/Whisper...\n" +
                    "If this never changes, check ffmpeg + model.";
                return;
            }

            if (mic.lastWhisperTime > 0f &&
                Time.time - mic.lastWhisperTime < whisperTimeout)
            {
                statusText.text =
                    "FFmpeg + Whisper OK.\n" +
                    $"Heard: \"{mic.lastWhisperText}\"";
            }
            else
            {
                statusText.text =
                    "Streaming mic to FFmpeg...\n" +
                    "Waiting for Whisper to recognize speech.";
            }
        }
    }

    public void ShowPanel()
    {
        if (panelRoot != null)
            panelRoot.SetActive(true);

        if (mic == null && MicrophoneManagerSingleton.Instance != null)
            mic = MicrophoneManagerSingleton.Instance.GetMicrophoneManager();

    }

    public void HidePanel()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }
}
