using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class NBackMinigameController : MonoBehaviour
{
    private enum NBackState
    {
        Cooldown,
        StartBuffer,
        ShowingSequence,
        WaitingForAnswer
    }

    [Header("UI")]
    public GameObject panelRoot;
    public TMP_Text phaseText;
    public TMP_Text letterText;
    public TMP_Text instructionText;
    public TMP_Text option1Text;
    public TMP_Text option2Text;

    [Header("Speech Feedback")]
    public TMP_Text feedbackText;

    [Header("Timing")]
    public float timeBetweenRounds = 0f;
    public float startBufferTime = 3f;
    public float letterShowDuration = 1f;
    public float letterGapDuration = 0.5f;
    public float answerDuration = 8f;

    [Header("Sequence Settings")]
    public int sequenceLength = 3;

    [Header("Timeout")]
    public bool neverTimeout = true;

    private NBackState state = NBackState.Cooldown;
    private float stateTimer = 0f;

    private char[] sequence;
    private char correctLetter;
    private char wrongLetter;
    private int correctOptionIndex;

    private char[] possibleLetters = { 'A', 'B', 'C', 'D' };

    private Coroutine sequenceCoroutine;

    private MicrophoneManager microphoneManager;

    // Trials
    private int trialsRemaining = 0;
    private int trialsTotal = 0;
    private bool runStarted = false;
    private float currentTrialStartTime;

    // Input mode
    private InputMode inputMode = InputMode.Keyboard;

    private void Start()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);

        if (feedbackText != null)
            feedbackText.enabled = false;

        state = NBackState.Cooldown;

        // Global settings
        if (MinigameManager.Instance != null)
        {
            answerDuration = MinigameManager.Instance.globalAnswerDuration;

            if (answerDuration <= 0f)
                neverTimeout = true;

            trialsTotal = Mathf.Max(1, MinigameManager.Instance.globalTrialsPerMinigame);
            trialsRemaining = trialsTotal;

            timeBetweenRounds = MinigameManager.Instance.globalTrialGap;
        }
        else
        {
            trialsTotal = trialsRemaining = 1;
        }

        stateTimer = timeBetweenRounds;

        // Input mode from config
        var cfg = RuntimeGameConfig.Instance;
        inputMode = (cfg != null) ? cfg.inputMode : InputMode.Keyboard;

        // Mic subscription only if in microphone mode
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
        stateTimer -= Time.deltaTime;

        switch (state)
        {
            case NBackState.Cooldown:
                HandleCooldown();
                break;

            case NBackState.StartBuffer:
                HandleStartBuffer();
                break;

            case NBackState.ShowingSequence:
                // sequence coroutine drives this state
                break;

            case NBackState.WaitingForAnswer:
                HandleWaitingForAnswer();
                break;
        }
    }

    private void HandleCooldown()
    {
        if (stateTimer > 0f)
            return;

        if (!runStarted)
        {
            if (MinigameManager.Instance != null &&
                MinigameManager.Instance.CanStartMinigame(MinigameType.NBack))
            {
                MinigameManager.Instance.NotifyMinigameStarted(MinigameType.NBack);
                runStarted = true;
                BeginStartBuffer();
            }
        }
        else
        {
            // We are in the middle of this minigame's block, between trials
            if (trialsRemaining > 0)
            {
                BeginStartBuffer();
            }
        }
    }

    private void BeginStartBuffer()
    {
        state = NBackState.StartBuffer;
        stateTimer = startBufferTime;

        if (panelRoot != null)
            panelRoot.SetActive(true);

        if (phaseText != null)
            phaseText.text = "GET READY";

        if (letterText != null)
            letterText.text = "";

        if (instructionText != null)
            instructionText.text = "Watch the letters, then answer.";

        if (option1Text != null) option1Text.text = "";
        if (option2Text != null) option2Text.text = "";

        if (feedbackText != null)
            feedbackText.enabled = false;
    }

    private void HandleStartBuffer()
    {
        int timeLeft = Mathf.CeilToInt(stateTimer);

        if (letterText != null)
            letterText.text = timeLeft.ToString();

        if (stateTimer <= 0f)
        {
            StartWatchPhase();
        }
    }

    private void StartWatchPhase()
    {
        state = NBackState.ShowingSequence;
        stateTimer = 0f;

        GenerateSequence();
        GenerateOptions();

        if (phaseText != null)
            phaseText.text = "WATCH THE ORDER";

        if (instructionText != null)
            instructionText.text = "Remember the letters.";

        if (option1Text != null) option1Text.text = "";
        if (option2Text != null) option2Text.text = "";

        if (feedbackText != null)
            feedbackText.enabled = false;

        if (sequenceCoroutine != null)
            StopCoroutine(sequenceCoroutine);
        sequenceCoroutine = StartCoroutine(PlaySequenceCoroutine());
    }

    private void StartAnswerPhase()
    {
        state = NBackState.WaitingForAnswer;
        stateTimer = answerDuration;

        // mark trial start (for response time)
        currentTrialStartTime = Time.time;

        if (phaseText != null)
            phaseText.text = "NOW ANSWER";

        if (instructionText != null)
            instructionText.text = "Which letter came BEFORE the last one?";

        if (letterText != null)
            letterText.text = "";

        if (feedbackText != null)
            feedbackText.enabled = false;

        if (correctOptionIndex == 1)
        {
            if (option1Text != null)
                option1Text.text = $"Say/Press 1: {correctLetter}";
            if (option2Text != null)
                option2Text.text = $"Say/Press 2: {wrongLetter}";
        }
        else
        {
            if (option1Text != null)
                option1Text.text = $"Say/Press 1: {wrongLetter}";
            if (option2Text != null)
                option2Text.text = $"Say/Press 2: {correctLetter}";
        }
    }

    private void HandleWaitingForAnswer()
    {
        int input = GetPlayerInput();
        if (input != 0)
        {
            HandleAnswer(input);
            return;
        }

        // mic answers come via HandleNumberRecognized

        if (neverTimeout || MinigameManager.Instance == null ||
            MinigameManager.Instance.globalAnswerDuration <= 0f)
            return;

        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f)
        {
            Timeout();
        }
    }

    private void HandleNumberRecognized(int number)
    {
        if (inputMode != InputMode.Microphone)
            return;

        Debug.Log($"[NBackGame] HandleNumberRecognized called with: {number}, state={state}");

        if (state == NBackState.WaitingForAnswer && (number == 1 || number == 2))
        {
            HandleAnswer(number);
        }
    }

    private void HandleUnrecognizedSpeech()
    {
        if (inputMode != InputMode.Microphone)
            return;

        if (state != NBackState.WaitingForAnswer)
            return;

        Debug.Log("[NBackGame] Unrecognized speech during answer – prompting repeat.");
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

    private void EndRound()
    {
        state = NBackState.Cooldown;
        stateTimer = timeBetweenRounds;

        if (panelRoot != null)
            panelRoot.SetActive(false);

        if (letterText != null)
            letterText.text = "";

        if (option1Text != null) option1Text.text = "";
        if (option2Text != null) option2Text.text = "";

        if (feedbackText != null)
            feedbackText.enabled = false;

        if (sequenceCoroutine != null)
        {
            StopCoroutine(sequenceCoroutine);
            sequenceCoroutine = null;
        }

        // trial bookkeeping
        trialsRemaining--;

        if (trialsRemaining <= 0)
        {
            if (MinigameManager.Instance != null)
                MinigameManager.Instance.NotifyMinigameEnded();
        }
        // else: stay in Cooldown, HandleCooldown will start next trial
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

    private void HandleAnswer(int chosenOption)
    {
        bool isCorrect = (chosenOption == correctOptionIndex);
        MinigameOutcome outcome = isCorrect ? MinigameOutcome.Correct : MinigameOutcome.Incorrect;

        int trialsDoneSoFar = trialsTotal - trialsRemaining + 1;
        if (trialsDoneSoFar > trialsTotal)
            trialsDoneSoFar = trialsTotal;

        string detail =
            $"[Trial {trialsDoneSoFar}/{trialsTotal}] NBack: sequence={new string(sequence)}, " +
            $"correctLetter={correctLetter}, wrongLetter={wrongLetter}, playerPick={chosenOption}";

        float responseTime = Time.time - currentTrialStartTime;

        if (MinigameManager.Instance != null)
        {
            MinigameManager.Instance.RegisterResult(
                MinigameType.NBack,
                outcome,
                isCorrect,
                detail,
                responseTime
            );
        }

        EndRound();
    }

    private void Timeout()
    {
        int trialsDoneSoFar = trialsTotal - trialsRemaining + 1;
        if (trialsDoneSoFar > trialsTotal)
            trialsDoneSoFar = trialsTotal;

        string detail = $"[Trial {trialsDoneSoFar}/{trialsTotal}] NBack: sequence={new string(sequence)} (Timeout)";

        float responseTime = Time.time - currentTrialStartTime;

        if (MinigameManager.Instance != null)
        {
            MinigameManager.Instance.RegisterResult(
                MinigameType.NBack,
                MinigameOutcome.Timeout,
                false,
                detail,
                responseTime
            );
        }

        EndRound();
    }

    private void GenerateSequence()
    {
        int len = Mathf.Max(2, sequenceLength);
        sequence = new char[len];

        for (int i = 0; i < len; i++)
        {
            sequence[i] = possibleLetters[Random.Range(0, possibleLetters.Length)];
        }
    }

    private void GenerateOptions()
    {
        int targetIndex = Mathf.Max(0, sequence.Length - 2);
        correctLetter = sequence[targetIndex];

        wrongLetter = correctLetter;
        while (wrongLetter == correctLetter)
        {
            wrongLetter = possibleLetters[Random.Range(0, possibleLetters.Length)];
        }

        correctOptionIndex = Random.Range(1, 3);
    }

    private IEnumerator PlaySequenceCoroutine()
    {
        if (letterText != null)
            letterText.text = "";

        for (int i = 0; i < sequence.Length; i++)
        {
            if (letterText != null)
                letterText.text = sequence[i].ToString();

            yield return new WaitForSeconds(letterShowDuration);

            if (letterText != null)
                letterText.text = "";

            yield return new WaitForSeconds(letterGapDuration);
        }

        StartAnswerPhase();
    }
}
