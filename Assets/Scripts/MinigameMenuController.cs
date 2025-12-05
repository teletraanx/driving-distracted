using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Debug = UnityEngine.Debug;

public class MinigameMenuController : MonoBehaviour
{
    [Header("UI References")]
    public TMP_Dropdown countDropdown;
    public Transform dropdownParent;
    public TMP_Dropdown dropdownPrefab;
    public TMP_Text warningText;
    public TMP_InputField trialsPerMinigameInputField;

    [Header("Input Mode")]
    public TMP_Dropdown inputModeDropdown;   // Keyboard / Microphone

    [Header("Timing UI")]
    public TMP_InputField answerTimeInputField;
    public TMP_InputField gapTimeInputField;
    public TMP_InputField trialGapInputField;

    [Header("Game Settings")]
    public string gameplaySceneName = "MainGame";
    public int minCount = 1;
    public int maxCount = 12;

    [Header("Microphone")]
    [SerializeField] TMP_Dropdown microphoneDropdown;

    private readonly List<TMP_Dropdown> dropdowns = new List<TMP_Dropdown>();

    private readonly List<(string label, MinigameType type)> minigameOptions =
        new List<(string, MinigameType)>
        {
            ("Math",       MinigameType.Math),
            ("Simon Says", MinigameType.SimonSays),
            ("Stroop",     MinigameType.Stroop),
            ("N-Back",     MinigameType.NBack)
        };

    private void Start()
    {
        if (warningText != null)
            warningText.text = "";

        var cfg = EnsureConfig();

        if (!cfg.systemChecksPerformed)
        {
            PerformSystemChecks();
            cfg.systemChecksPerformed = true;
        }

        SetupCountDropdown();
        SetupTimingFields();
        InitMicrophoneDrowdown();
        SetupInputModeDropdown(cfg);   // 👈 use cfg here
    }

    // -----------------------------------------------------
    //  Count & ordering of minigames
    // -----------------------------------------------------
    private void SetupCountDropdown()
    {
        if (countDropdown == null)
        {
            Debug.LogWarning("CountDropdown is not assigned.");
            return;
        }

        countDropdown.ClearOptions();

        List<TMP_Dropdown.OptionData> options = new List<TMP_Dropdown.OptionData>();
        for (int i = minCount; i <= maxCount; i++)
        {
            options.Add(new TMP_Dropdown.OptionData(i.ToString()));
        }

        countDropdown.AddOptions(options);

        int defaultCount = 4;
        int defaultIndex = Mathf.Clamp(defaultCount, minCount, maxCount) - minCount;
        countDropdown.value = defaultIndex;
        countDropdown.RefreshShownValue();

        int initialCount = defaultIndex + minCount;
        RebuildDropdowns(initialCount);
    }

    private void RebuildDropdowns(int count)
    {
        foreach (var dd in dropdowns)
        {
            if (dd != null)
                Destroy(dd.gameObject);
        }
        dropdowns.Clear();

        for (int i = 0; i < count; i++)
        {
            TMP_Dropdown dd = Instantiate(dropdownPrefab, dropdownParent);
            dd.gameObject.name = $"MinigameDropdown_{i + 1}";

            dd.ClearOptions();

            List<TMP_Dropdown.OptionData> options = new List<TMP_Dropdown.OptionData>();
            foreach (var opt in minigameOptions)
            {
                options.Add(new TMP_Dropdown.OptionData(opt.label));
            }
            dd.AddOptions(options);

            dd.value = i % minigameOptions.Count;
            dd.RefreshShownValue();

            dropdowns.Add(dd);
        }
    }

    public void OnCountDropdownChanged(int optionIndex)
    {
        int count = optionIndex + minCount;

        if (warningText != null)
            warningText.text = "";

        RebuildDropdowns(count);
    }

    // -----------------------------------------------------
    //  Timing + trials fields
    // -----------------------------------------------------
    private void SetupTimingFields()
    {
        float defaultAnswer = 0f;
        float defaultGap = 5f;
        int defaultTrials = 1;
        float defaultTrialGap = 1f;

        if (MinigameManager.Instance != null)
        {
            defaultAnswer = MinigameManager.Instance.globalAnswerDuration;
            defaultGap = MinigameManager.Instance.globalMinigameGap;
            defaultTrials = Mathf.Max(1, MinigameManager.Instance.globalTrialsPerMinigame);
            defaultTrialGap = MinigameManager.Instance.globalTrialGap;
        }

        if (answerTimeInputField != null)
            answerTimeInputField.text = defaultAnswer.ToString("0");

        if (gapTimeInputField != null)
            gapTimeInputField.text = defaultGap.ToString("0");

        if (trialsPerMinigameInputField != null)
            trialsPerMinigameInputField.text = defaultTrials.ToString();

        if (trialGapInputField != null)
            trialGapInputField.text = defaultTrialGap.ToString("0.0");
    }

    // -----------------------------------------------------
    //  Input mode dropdown (Keyboard / Mic)
    // -----------------------------------------------------
    private void SetupInputModeDropdown(RuntimeGameConfig cfg)
    {
        if (inputModeDropdown == null)
            return;

        inputModeDropdown.ClearOptions();

        var options = new List<TMP_Dropdown.OptionData>
        {
            new TMP_Dropdown.OptionData("Keyboard"),
            new TMP_Dropdown.OptionData("Microphone")
        };
        inputModeDropdown.AddOptions(options);

        int index = (cfg.inputMode == InputMode.Microphone) ? 1 : 0;
        inputModeDropdown.SetValueWithoutNotify(index);
        inputModeDropdown.RefreshShownValue();

        // If you want cfg to update immediately when changed:
        inputModeDropdown.onValueChanged.AddListener(i =>
        {
            cfg.inputMode = (i == 1) ? InputMode.Microphone : InputMode.Keyboard;
        });
    }

    // -----------------------------------------------------
    //  Start button
    // -----------------------------------------------------
    public void OnStartButtonPressed()
    {
        if (MinigameManager.Instance == null)
        {
            if (warningText != null)
                warningText.text = "No MinigameManager found!";
            Debug.LogError("MinigameManager.Instance is null. Make sure it exists in the Main Menu scene.");
            return;
        }

        if (dropdowns.Count == 0)
        {
            if (warningText != null)
                warningText.text = "Please choose at least one minigame.";
            return;
        }

        float answerTime = 0f;
        float gapTime = 5f;
        int trialsPerMinigame = 1;
        float trialGap = 1f;

        if (answerTimeInputField != null &&
            float.TryParse(answerTimeInputField.text, out float parsedAnswer) &&
            parsedAnswer > 0.1f)
        {
            answerTime = parsedAnswer;
        }

        if (gapTimeInputField != null &&
            float.TryParse(gapTimeInputField.text, out float parsedGap) &&
            parsedGap >= 0f)
        {
            gapTime = parsedGap;
        }

        if (trialsPerMinigameInputField != null &&
            int.TryParse(trialsPerMinigameInputField.text, out int parsedTrials) &&
            parsedTrials > 0)
        {
            trialsPerMinigame = parsedTrials;
        }

        if (trialGapInputField != null &&
            float.TryParse(trialGapInputField.text, out float parsedTrialGap) &&
            parsedTrialGap >= 0f)
        {
            trialGap = parsedTrialGap;
        }

        MinigameManager.Instance.globalAnswerDuration = answerTime;
        MinigameManager.Instance.globalMinigameGap = gapTime;
        MinigameManager.Instance.globalTrialsPerMinigame = trialsPerMinigame;
        MinigameManager.Instance.globalTrialGap = trialGap;

        // Input mode is already stored in RuntimeGameConfig by the dropdown callback.
        var cfg = EnsureConfig();

        // 🔊 With the new always-running mic pipeline,
        // we do NOT need to start it here anymore.
        // It’s already running from the singleton + MicrophoneManager.Start().

        // Build ordered sequence of minigames
        MinigameType[] order = new MinigameType[dropdowns.Count];

        for (int i = 0; i < dropdowns.Count; i++)
        {
            int idx = dropdowns[i].value;
            if (idx < 0 || idx >= minigameOptions.Count)
                idx = 0;

            order[i] = minigameOptions[idx].type;
        }

        MinigameManager.Instance.sequenceOrder = order;

        MinigameManager.Instance.ResetRunState();

        if (warningText != null)
            warningText.text = "";

        SceneManager.LoadScene(gameplaySceneName);
    }

    // -----------------------------------------------------
    //  Microphone device selection
    // -----------------------------------------------------
    void InitMicrophoneDrowdown()
    {
        if (!microphoneDropdown) return;

        microphoneDropdown.ClearOptions();
        var devices = Microphone.devices;

        if (devices.Length == 0)
        {
            microphoneDropdown.AddOptions(new List<string> { "No Microphones Found" });
            microphoneDropdown.interactable = false;
            return;
        }

        var options = new List<string>(devices);
        microphoneDropdown.AddOptions(options);
        microphoneDropdown.onValueChanged.AddListener(OnMicrophoneChanged);

        var cfg = EnsureConfig();
        if (cfg.selectedMicrophoneIndex >= 0 && cfg.selectedMicrophoneIndex < devices.Length)
        {
            microphoneDropdown.SetValueWithoutNotify(cfg.selectedMicrophoneIndex);
        }
        else
        {
            microphoneDropdown.SetValueWithoutNotify(0);
            cfg.selectedMicrophoneIndex = 0;
        }
    }

    void OnMicrophoneChanged(int index)
    {
        var cfg = EnsureConfig();
        cfg.selectedMicrophoneIndex = index;
    }

    // -----------------------------------------------------
    //  Config + app exit
    // -----------------------------------------------------
    RuntimeGameConfig EnsureConfig()
    {
        var cfg = RuntimeGameConfig.Instance;
        if (cfg == null)
        {
            var go = new GameObject("RuntimeGameConfig");
            cfg = go.AddComponent<RuntimeGameConfig>();
        }
        return cfg;
    }

    public void OnExitApplicationPressed()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // -----------------------------------------------------
    //  System checks (unchanged from your version)
    // -----------------------------------------------------
    void PerformSystemChecks()
    {
        if (!IsSupportedOS())
        {
            ShowMessageBox("Unsupported Operating System",
                "This application requires Windows or Linux to run speech recognition features.\n\n" +
                "Please use a supported operating system to access all functionality.");
            SetSpeechRecognitionAvailable(false);
            return;
        }

        if (!HasNvidiaGPU())
        {
            ShowMessageBox("No NVIDIA GPU Detected",
                "Speech recognition requires an NVIDIA GPU for optimal performance.\n\n" +
                "Please ensure you have an NVIDIA graphics card installed to use speech-to-text features.");
            SetSpeechRecognitionAvailable(false);
            return;
        }

        if (!CheckFFmpegInstallation())
        {
            ShowMessageBox("FFmpeg Not Found",
                "FFmpeg is required for speech recognition but was not found on your system.\n\n" +
                "Please install FFmpeg and ensure it's in your system PATH to use speech-to-text features.\n" +
                "Instructions on how to install FFmpeg can be found in the included README file.");
            SetSpeechRecognitionAvailable(false);
            return;
        }

        if (!CheckWhisperModel())
        {
            ShowMessageBox("Whisper Model Missing",
                "The required Whisper speech recognition model was not found.\n\n" +
                "Please ensure that the model file exists in the StreamingAssets/Whisper directory.");
            SetSpeechRecognitionAvailable(false);
            return;
        }

        SetSpeechRecognitionAvailable(true);
    }

    bool IsSupportedOS()
    {
        var os = SystemInfo.operatingSystem;
        if (string.IsNullOrEmpty(os)) return false;
        return os.Contains("Windows") || os.Contains("Linux");
    }

    bool HasNvidiaGPU()
    {
        var gpuName = SystemInfo.graphicsDeviceName ?? "";
        return gpuName.Contains("NVIDIA") || gpuName.Contains("GeForce") || gpuName.Contains("Quadro");
    }

    bool CheckFFmpegInstallation()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = "-version",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using (var process = Process.Start(startInfo))
            {
                if (process == null) return false;
                process.WaitForExit(5000);
                if (!process.HasExited)
                {
                    try { process.Kill(); } catch { }
                    return false;
                }
                return process.ExitCode == 0;
            }
        }
        catch
        {
            return false;
        }
    }

    bool CheckWhisperModel()
    {
        string modelPath = GetWhisperModelPath();
        if (string.IsNullOrEmpty(modelPath)) return false;
        return File.Exists(modelPath);
    }

    string GetWhisperModelPath()
    {
#if UNITY_EDITOR
        return Path.Combine(Application.dataPath, "Whisper", "ggml-medium.en.bin");
#else
        return Path.Combine(Application.streamingAssetsPath, "Whisper", "ggml-medium.en.bin");
#endif
    }

    void ShowMessageBox(string title, string message)
    {
        var canvas = new GameObject("MessageBoxCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas.transform.SetParent(transform, false);

        var canvasComp = canvas.GetComponent<Canvas>();
        canvasComp.renderMode = RenderMode.ScreenSpaceOverlay;

        int uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer != -1) canvas.layer = uiLayer;

        var canvasRect = canvas.GetComponent<RectTransform>();
        canvasRect.anchorMin = Vector2.zero;
        canvasRect.anchorMax = Vector2.one;
        canvasRect.sizeDelta = Vector2.zero;
        canvasRect.anchoredPosition = Vector2.zero;

        var bgPanel = new GameObject("Background", typeof(Image));
        bgPanel.transform.SetParent(canvas.transform, false);
        var bgRect = bgPanel.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;

        var bgImage = bgPanel.GetComponent<Image>();
        bgImage.color = new Color(0, 0, 0, 0.7f);

        var msgPanel = new GameObject("MessagePanel", typeof(Image));
        msgPanel.transform.SetParent(canvas.transform, false);
        var msgRect = msgPanel.GetComponent<RectTransform>();
        msgRect.anchorMin = new Vector2(0.2f, 0.3f);
        msgRect.anchorMax = new Vector2(0.8f, 0.7f);
        msgRect.sizeDelta = Vector2.zero;

        var msgImage = msgPanel.GetComponent<Image>();
        msgImage.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);

        var titleText = new GameObject("Title", typeof(TextMeshProUGUI));
        titleText.transform.SetParent(msgPanel.transform, false);
        var titleRect = titleText.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.1f, 0.7f);
        titleRect.anchorMax = new Vector2(0.9f, 0.9f);
        titleRect.sizeDelta = Vector2.zero;

        var titleComponent = titleText.GetComponent<TextMeshProUGUI>();
        titleComponent.text = title;
        titleComponent.fontSize = 24;
        titleComponent.fontStyle = FontStyles.Bold;
        titleComponent.color = Color.white;
        titleComponent.alignment = TextAlignmentOptions.Center;

        var msgText = new GameObject("Message", typeof(TextMeshProUGUI));
        msgText.transform.SetParent(msgPanel.transform, false);
        var msgRect2 = msgText.GetComponent<RectTransform>();
        msgRect2.anchorMin = new Vector2(0.1f, 0.2f);
        msgRect2.anchorMax = new Vector2(0.9f, 0.7f);
        msgRect2.sizeDelta = Vector2.zero;

        var msgComponent = msgText.GetComponent<TextMeshProUGUI>();
        msgComponent.text = message;
        msgComponent.fontSize = 16;
        msgComponent.color = Color.white;
        msgComponent.alignment = TextAlignmentOptions.Center;
        msgComponent.richText = true;

        var buttonGO = new GameObject("DismissButton", typeof(Button), typeof(Image));
        buttonGO.transform.SetParent(msgPanel.transform, false);
        var btnRect = buttonGO.GetComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0.3f, 0.05f);
        btnRect.anchorMax = new Vector2(0.7f, 0.2f);
        btnRect.sizeDelta = Vector2.zero;

        var btnImage = buttonGO.GetComponent<Image>();
        btnImage.color = new Color(0.3f, 0.6f, 1f);

        var buttonText = new GameObject("ButtonText", typeof(TextMeshProUGUI));
        buttonText.transform.SetParent(buttonGO.transform, false);
        var btnTextRect = buttonText.GetComponent<RectTransform>();
        btnTextRect.anchorMin = Vector2.zero;
        btnTextRect.anchorMax = Vector2.one;
        btnTextRect.sizeDelta = Vector2.zero;

        var btnTextComponent = buttonText.GetComponent<TextMeshProUGUI>();
        btnTextComponent.text = "OK";
        btnTextComponent.fontSize = 18;
        btnTextComponent.color = Color.white;
        btnTextComponent.alignment = TextAlignmentOptions.Center;

        var button = buttonGO.GetComponent<Button>();
        button.onClick.AddListener(() =>
        {
            Destroy(canvas);
        });
    }

    void SetSpeechRecognitionAvailable(bool available)
    {
        var cfg = EnsureConfig();
        if (cfg != null)
        {
            cfg.speechRecognitionAvailable = available;
        }
    }
}
