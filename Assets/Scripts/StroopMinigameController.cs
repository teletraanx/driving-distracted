using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class StroopMinigameController : MonoBehaviour
{
    [Header("UI")]
    public GameObject panelRoot;
    public TMP_Text wordText;
    public TMP_Text instructionText;

    [Header("Speech Feedback")]
    public TMP_Text feedbackText;

    [Header("Timing")]
    public float timeBetweenRounds = 0f;
    public float questionDuration = 8f;

    private float cooldownTimer = 0f;
    private float questionTimer = 0f;
    private bool questionActive = false;

    private readonly string[] colorWords = { "RED", "GREEN", "BLUE" };
    private readonly Color[] colorValues = { Color.red, Color.green, Color.blue };

    private int meaningIndex;
    private int colorIndex;
    private bool isMatch;

    public bool neverTimeout = true;

    private MicrophoneManager microphoneManager;

    private int trialsRemaining = 0;
    private int trialsTotal = 0;
    private bool runStarted = false;
    private float currentTrialStartTime;

    private InputMode inputMode = InputMode.Keyboard;

    private void Start()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);

        if (feedbackText != null)
            feedbackText.enabled = false;

        cooldownTimer = timeBetweenRounds;

        if (MinigameManager.Instance != null)
        {
            questionDuration = MinigameManager.Instance.globalAnswerDuration;

            if (questionDuration <= 0f)
                neverTimeout = true;

            trialsTotal = Mathf.Max(1, MinigameManager.Instance.globalTrialsPerMinigame);
            trialsRemaining = trialsTotal;
            timeBetweenRounds = MinigameManager.Instance.globalTrialGap;
            cooldownTimer = timeBetweenRounds;
        }
        else
        {
            trialsTotal = trialsRemaining = 1;
        }

        var cfg = RuntimeGameConfig.Instance;
        inputMode = (cfg != null) ? cfg.inputMode : InputMode.Keyboard;

        if (inputMode == InputMode.Microphone && MicrophoneManagerSingleton.Instance != null)
        {
            microphoneManager = MicrophoneManagerSingleton.Instance.GetMicrophoneManager();
            if (microphoneManager != null)
            {
                microphoneManager.OnNumberRecognized += HandleNumberRecognized;
                microphoneManager.OnUnrecognizedSpeech += HandleUnrecognizedSpeech;
            }
        }
    }

    private void OnDestroy()
    {
        if (microphoneManager != null)
        {
            microphoneManager.OnNumberRecognized -= HandleNumberRecognized;
            microphoneManager.OnUnrecognizedSpeech -= HandleUnrecognizedSpeech;
        }
    }

    private void Update()
    {
        if (!questionActive)
        {
            cooldownTimer -= Time.deltaTime;

            if (!runStarted)
            {
                if (cooldownTimer <= 0f &&
                    MinigameManager.Instance != null &&
                    MinigameManager.Instance.CanStartMinigame(MinigameType.Stroop))
                {
                    MinigameManager.Instance.NotifyMinigameStarted(MinigameType.Stroop);
                    runStarted = true;
                    StartQuestion();
                }
            }
            else
            {
                if (cooldownTimer <= 0f && trialsRemaining > 0)
                {
                    StartQuestion();
                }
            }
        }
        else
        {
            int input = GetPlayerInput();
            if (input != 0)
            {
                HandleAnswer(input);
                return;
            }


            if (neverTimeout || MinigameManager.Instance == null ||
                MinigameManager.Instance.globalAnswerDuration <= 0f)
                return;

            questionTimer -= Time.deltaTime;
            if (questionTimer <= 0f)
            {
                Timeout();
            }
        }
    }

    private void HandleNumberRecognized(int number)
    {
        if (inputMode != InputMode.Microphone)
            return;

        Debug.Log($"[StroopGame] HandleNumberRecognized called with: {number}, questionActive={questionActive}");

        if (questionActive && (number == 1 || number == 2))
        {
            HandleAnswer(number);
        }
    }

    private void HandleUnrecognizedSpeech()
    {
        if (inputMode != InputMode.Microphone)
            return;

        if (!questionActive)
            return;

        Debug.Log("[StroopGame] Unrecognized speech during question – prompting repeat.");
        StartCoroutine(FlashFeedbackText());
    }

    private IEnumerator FlashFeedbackText()
    {
        if (feedbackText == null)
            yield break;

        feedbackText.text = "Say your answer again";
        feedbackText.enabled = true;

        Color original = feedbackText.color;

        for (int i = 0; i < 4; i++)
        {
            feedbackText.color = Color.red;
            yield return new WaitForSeconds(0.2f);

            feedbackText.color = original;
            yield return new WaitForSeconds(0.2f);
        }

        feedbackText.enabled = false;
    }

    private int GetPlayerInput()
    {
        if (inputMode != InputMode.Keyboard)
            return 0;

        if (Keyboard.current == null) return 0;

        if (Keyboard.current.digit1Key.wasPressedThisFrame) return 1;
        if (Keyboard.current.digit2Key.wasPressedThisFrame) return 2;

        if (Keyboard.current.numpad1Key.wasPressedThisFrame) return 1;
        if (Keyboard.current.numpad2Key.wasPressedThisFrame) return 2;

        return 0;
    }

    private void StartQuestion()
    {
        if (trialsRemaining <= 0)
            return;

        questionActive = true;

        if (!neverTimeout && MinigameManager.Instance != null &&
            MinigameManager.Instance.globalAnswerDuration > 0f)
        {
            questionDuration = MinigameManager.Instance.globalAnswerDuration;
            questionTimer = questionDuration;
        }

        currentTrialStartTime = Time.time;

        if (panelRoot != null)
            panelRoot.SetActive(true);

        if (feedbackText != null)
            feedbackText.enabled = false;

        GenerateTrial();
        UpdateUI();
    }

    private void EndQuestion()
    {
        questionActive = false;
        cooldownTimer = timeBetweenRounds;

        if (panelRoot != null)
            panelRoot.SetActive(false);

        if (feedbackText != null)
            feedbackText.enabled = false;

        trialsRemaining--;

        if (trialsRemaining <= 0)
        {
            if (MinigameManager.Instance != null)
                MinigameManager.Instance.NotifyMinigameEnded();
        }
       
    }

    private void GenerateTrial()
    {
        meaningIndex = Random.Range(0, colorWords.Length);

        isMatch = Random.value < 0.5f;

        if (isMatch)
        {
            colorIndex = meaningIndex;
        }
        else
        {
            colorIndex = Random.Range(0, colorValues.Length);
            if (colorIndex == meaningIndex)
            {
                colorIndex = (colorIndex + 1) % colorValues.Length;
            }
        }
    }

    private void UpdateUI()
    {
        if (wordText != null)
        {
            wordText.text = colorWords[meaningIndex];
            wordText.color = colorValues[colorIndex];
        }

        if (instructionText != null)
        {
            instructionText.text =
                "Does the WORD match the COLOR?\n" +
                "Say/Press 1 = YES\n" +
                "Say/Press 2 = NO";
        }
    }

    private void HandleAnswer(int input)
    {
        bool playerSaysMatch = (input == 1);
        bool correct = (playerSaysMatch == isMatch);

        MinigameOutcome outcome = correct ? MinigameOutcome.Correct : MinigameOutcome.Incorrect;

        int trialsDoneSoFar = trialsTotal - trialsRemaining + 1;
        if (trialsDoneSoFar > trialsTotal)
            trialsDoneSoFar = trialsTotal;

        string detail =
            $"[Trial {trialsDoneSoFar}/{trialsTotal}] " +
            $"Stroop: Word={colorWords[meaningIndex]}, Color={colorWords[colorIndex]}, " +
            $"IsMatch={isMatch}, PlayerSaysMatch={playerSaysMatch}";

        float responseTime = Time.time - currentTrialStartTime;

        if (MinigameManager.Instance != null)
        {
            MinigameManager.Instance.RegisterResult(
                MinigameType.Stroop,
                outcome,
                correct,
                detail,
                responseTime
            );
        }

        EndQuestion();
    }

    private void Timeout()
    {
        int trialsDoneSoFar = trialsTotal - trialsRemaining + 1;
        if (trialsDoneSoFar > trialsTotal)
            trialsDoneSoFar = trialsTotal;

        string detail =
            $"[Trial {trialsDoneSoFar}/{trialsTotal}] " +
            $"Stroop: Word={colorWords[meaningIndex]}, Color={colorWords[colorIndex]} (Timeout)";

        float responseTime = Time.time - currentTrialStartTime;

        if (MinigameManager.Instance != null)
        {
            MinigameManager.Instance.RegisterResult(
                MinigameType.Stroop,
                MinigameOutcome.Timeout,
                false,
                detail,
                responseTime
            );
        }

        EndQuestion();
    }
}
