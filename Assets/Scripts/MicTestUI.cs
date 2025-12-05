using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MicTestUI : MonoBehaviour
{
    [Header("UI")]
    public GameObject panelRoot;      // Optional: turn the whole panel on/off
    public Slider levelSlider;        // Slider with min=0, max=1
    public TMP_Text statusText;       // "Say something..." / "Heard: ..."

    [Header("Tuning")]
    public float levelGain = 6f;      // Boost mic level into 0–1ish
    public float smoothSpeed = 10f;   // Smoothing for the bar
    public float whisperTimeout = 5f; // How long until we say "waiting..." again

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

        // ---- Mic level bar ----
        float target = Mathf.Clamp01(mic.currentLevel * levelGain);
        smoothedLevel = Mathf.Lerp(smoothedLevel, target, Time.deltaTime * smoothSpeed);

        if (levelSlider != null)
            levelSlider.value = smoothedLevel;

        // ---- FFmpeg + Whisper status ----
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

    // Optional UI toggle
    public void ShowPanel()
    {
        if (panelRoot != null)
            panelRoot.SetActive(true);

        if (mic == null && MicrophoneManagerSingleton.Instance != null)
            mic = MicrophoneManagerSingleton.Instance.GetMicrophoneManager();

        // ❌ No call to StartMicPipelineIfNeeded() anymore
    }

    public void HidePanel()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }
}
