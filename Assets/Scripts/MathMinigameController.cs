using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class MathMinigameController : MonoBehaviour
{
    [Header("UI References")]
    public GameObject panelRoot;
    public TMP_Text questionText;
    public TMP_Text option1Text;
    public TMP_Text option2Text;

    [Header("Speech Feedback")]
    public TMP_Text feedbackText;   // "Say your answer again"

    [Header("Timing")]
    public float timeBetweenQuestions = 5f;  // gap between trials of THIS minigame
    public float questionDuration = 8f;      // per-trial answer time (overridden by global)

    private float cooldownTimer = 0f;
    private float questionTimer = 0f;
    private bool questionActive = false;

    private int correctOptionIndex;
    private int correctAnswer;
    private int option1Value;
    private int option2Value;
    private string currentQuestion;

    [Header("Timeout")]
    public bool neverTimeout = true;   // if true, no timeout – only correct/incorrect

    // Trials for this minigame
    private int trialsRemaining = 0;
    private int trialsTotal = 0;
    private bool runStarted = false;      // this minigame has begun its block
    private float currentTrialStartTime;  // for response-time measurement

    // Input mode
    private InputMode inputMode = InputMode.Keyboard;

    // Mic
    private MicrophoneManager microphoneManager;

    private void Start()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);

        if (feedbackText != null)
            feedbackText.enabled = false;

        cooldownTimer = timeBetweenQuestions;

        // Trials: how many questions this minigame will ask
        if (MinigameManager.Instance != null)
        {
            // pull global per-minigame trial count
            trialsTotal = Mathf.Max(1, MinigameManager.Instance.globalTrialsPerMinigame);
            trialsRemaining = trialsTotal;

            // pull global answer duration
            questionDuration = MinigameManager.Instance.globalAnswerDuration;

            if (questionDuration <= 0f)
            {
                neverTimeout = true;
            }
            else
            {
                neverTimeout = false;
            }

            timeBetweenQuestions = MinigameManager.Instance.globalTrialGap;
            cooldownTimer = timeBetweenQuestions;
        }
        else
        {
            trialsTotal = trialsRemaining = 1;
        }

        // Input mode from config
        var cfg = RuntimeGameConfig.Instance;
        inputMode = cfg != null ? cfg.inputMode : InputMode.Keyboard;

        // Only hook mic if we’re in microphone mode
        if (inputMode == InputMode.Microphone && MicrophoneManagerSingleton.Instance != null)
        {
            Debug.Log("[MathGame] Mic singleton found.");
            microphoneManager = MicrophoneManagerSingleton.Instance.GetMicrophoneManager();
            if (microphoneManager != null)
            {
                Debug.Log("[MathGame] Subscribing to OnNumberRecognized and OnUnrecognizedSpeech.");
                microphoneManager.OnNumberRecognized += HandleNumberRecognized;
                microphoneManager.OnUnrecognizedSpeech += HandleUnrecognizedSpeech;
            }
            else
            {
                Debug.LogWarning("[MathGame] MicrophoneManager from singleton is null.");
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
            // Between trials
            cooldownTimer -= Time.deltaTime;

            if (!runStarted)
            {
                // First time this minigame is allowed to start
                if (cooldownTimer <= 0f &&
                    MinigameManager.Instance != null &&
                    MinigameManager.Instance.CanStartMinigame(MinigameType.Math))
                {
                    MinigameManager.Instance.NotifyMinigameStarted(MinigameType.Math);
                    runStarted = true;
                    StartNewQuestion();
                }
            }
            else
            {
                // We are in this minigame’s block; handle subsequent trials
                if (cooldownTimer <= 0f && trialsRemaining > 0)
                {
                    StartNewQuestion();
                }
            }
        }
        else
        {
            // Question is active – watch for input

            // 1) Keyboard input (if in keyboard mode)
            int chosenIndex = GetAnswerInput();
            if (chosenIndex != 0)
            {
                HandleAnswer(chosenIndex);
                return;
            }

            // 2) Mic answers come in via HandleNumberRecognized

            // 3) Timeout logic (only if globally configured)
            if (neverTimeout ||
                MinigameManager.Instance == null ||
                MinigameManager.Instance.globalAnswerDuration <= 0f)
            {
                return; // no timeout
            }

            questionTimer -= Time.deltaTime;
            if (questionTimer <= 0f)
            {
                HandleTimeout();
            }
        }
    }

    private int GetAnswerInput()
    {
        // Only read keyboard in keyboard mode
        if (inputMode != InputMode.Keyboard)
            return 0;

        if (Keyboard.current == null)
            return 0;

        if (Keyboard.current.digit1Key.wasPressedThisFrame)
            return 1;
        if (Keyboard.current.digit2Key.wasPressedThisFrame)
            return 2;

        if (Keyboard.current.numpad1Key.wasPressedThisFrame)
            return 1;
        if (Keyboard.current.numpad2Key.wasPressedThisFrame)
            return 2;

        return 0;
    }

    private void HandleNumberRecognized(int number)
    {
        if (inputMode != InputMode.Microphone)
            return;

        Debug.Log($"[MathGame] HandleNumberRecognized called with: {number}, questionActive={questionActive}");

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

        Debug.Log("[MathGame] Unrecognized speech while question active – prompting user to repeat.");
        StartCoroutine(FlashFeedbackText());
    }

    private IEnumerator FlashFeedbackText()
    {
        if (feedbackText == null)
            yield break;

        feedbackText.text = "Say your answer again";
        feedbackText.enabled = true;

        Color originalColor = feedbackText.color;

        for (int i = 0; i < 4; i++)
        {
            feedbackText.color = Color.red;
            yield return new WaitForSeconds(0.2f);

            feedbackText.color = originalColor;
            yield return new WaitForSeconds(0.2f);
        }

        feedbackText.enabled = false;
    }

    private void StartNewQuestion()
    {
        if (trialsRemaining <= 0)
            return;

        questionActive = true;
        currentTrialStartTime = Time.time;

        // Set timer only if we actually want timeouts
        if (!neverTimeout &&
            MinigameManager.Instance != null &&
            MinigameManager.Instance.globalAnswerDuration > 0f)
        {
            questionDuration = MinigameManager.Instance.globalAnswerDuration;
            questionTimer = questionDuration;
        }

        if (panelRoot != null)
            panelRoot.SetActive(true);

        if (feedbackText != null)
            feedbackText.enabled = false;

        GenerateQuestion();
        UpdateUI();
    }

    private void EndQuestion()
    {
        // End this trial
        questionActive = false;
        cooldownTimer = timeBetweenQuestions;

        if (panelRoot != null)
            panelRoot.SetActive(false);

        if (feedbackText != null)
            feedbackText.enabled = false;

        // Bookkeeping for trials
        trialsRemaining--;

        if (trialsRemaining <= 0)
        {
            // Done with this minigame
            if (MinigameManager.Instance != null)
                MinigameManager.Instance.NotifyMinigameEnded();
        }
        // else: Update() will wait for cooldown and then StartNewQuestion()
    }

    private void HandleAnswer(int chosenIndex)
    {
        bool isCorrect = (chosenIndex == correctOptionIndex);
        var outcome = isCorrect ? MinigameOutcome.Correct : MinigameOutcome.Incorrect;

        string chosenValue = chosenIndex == 1 ? option1Value.ToString() : option2Value.ToString();

        int trialsDoneSoFar = trialsTotal - trialsRemaining + 1;
        if (trialsDoneSoFar > trialsTotal)
            trialsDoneSoFar = trialsTotal;

        string detail =
            $"[Trial {trialsDoneSoFar}/{trialsTotal}] {currentQuestion}  " +
            $"Correct={correctAnswer}, Chosen={chosenValue}";

        float responseTime = Time.time - currentTrialStartTime;

        if (MinigameManager.Instance != null)
        {
            MinigameManager.Instance.RegisterResult(
                MinigameType.Math,
                outcome,
                isCorrect,
                detail,
                responseTime
            );
        }

        EndQuestion();
    }

    private void HandleTimeout()
    {
        int trialsDoneSoFar = trialsTotal - trialsRemaining + 1;
        if (trialsDoneSoFar > trialsTotal)
            trialsDoneSoFar = trialsTotal;

        string detail =
            $"[Trial {trialsDoneSoFar}/{trialsTotal}] {currentQuestion}  " +
            $"TIMED OUT (Correct={correctAnswer})";

        float responseTime = Time.time - currentTrialStartTime;

        if (MinigameManager.Instance != null)
        {
            MinigameManager.Instance.RegisterResult(
                MinigameType.Math,
                MinigameOutcome.Timeout,
                false,
                detail,
                responseTime
            );
        }

        EndQuestion();
    }

    private void GenerateQuestion()
    {
        int a = Random.Range(1, 10);
        int b = Random.Range(1, 10);

        int op = Random.Range(0, 3);
        char opChar = '+';

        switch (op)
        {
            case 0:
                opChar = '+';
                correctAnswer = a + b;
                break;
            case 1:
                opChar = '-';
                correctAnswer = a - b;
                break;
            case 2:
                opChar = '*';
                correctAnswer = a * b;
                break;
        }

        currentQuestion = $"{a} {opChar} {b} = ?";

        // Decide which option is correct
        correctOptionIndex = Random.Range(1, 3);

        int wrongAnswer = correctAnswer;
        int offset = Random.Range(1, 4);
        if (Random.value < 0.5f)
            wrongAnswer += offset;
        else
            wrongAnswer -= offset;

        if (correctOptionIndex == 1)
        {
            option1Value = correctAnswer;
            option2Value = wrongAnswer;
        }
        else
        {
            option1Value = wrongAnswer;
            option2Value = correctAnswer;
        }
    }

    private void UpdateUI()
    {
        if (questionText != null)
            questionText.text = currentQuestion;

        if (option1Text != null)
            option1Text.text = $"Say/Press 1: {option1Value}";

        if (option2Text != null)
            option2Text.text = $"Say/Press 2: {option2Value}";
    }
}
