using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

public class SimonMinigameController : MonoBehaviour
{
    private enum SimonState
    {
        Cooldown,
        StartBuffer,
        ShowingSequence,
        WaitingForAnswer
    }

    [Header("UI")]
    public GameObject panelRoot;
    public Image colorDisplay;
    public TMP_Text phaseText;
    public TMP_Text option1Text;
    public TMP_Text option2Text;

    [Header("Speech Feedback")]
    public TMP_Text feedbackText;

    [Header("Timing")]
    public float timeBetweenRounds = 0f;
    public float startBufferTime = 3f;
    public float colorShowDuration = 0.6f;
    public float colorGapDuration = 0.2f;
    public float answerDuration = 8f;

    private SimonState state = SimonState.Cooldown;
    private float stateTimer = 0f;

    private int[] sequence;
    private string correctSequenceStr;
    private string wrongSequenceStr;
    private int correctOptionIndex;

    private readonly string[] colorWords = { "RED", "GREEN", "BLUE" };
    private readonly Color[] colorValues = { Color.red, Color.green, Color.blue };

    private Coroutine sequenceCoroutine;

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

        if (colorDisplay != null)
            colorDisplay.color = Color.black;

        if (feedbackText != null)
            feedbackText.enabled = false;

        state = SimonState.Cooldown;
        

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
        stateTimer -= Time.deltaTime;

        switch (state)
        {
            case SimonState.Cooldown:
                HandleCooldown();
                break;

            case SimonState.StartBuffer:
                HandleStartBuffer();
                break;

            case SimonState.ShowingSequence:
                break;

            case SimonState.WaitingForAnswer:
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
                MinigameManager.Instance.CanStartMinigame(MinigameType.SimonSays))
            {
                if (MinigameManager.Instance != null)
                {
                    trialsTotal = Mathf.Max(1, MinigameManager.Instance.globalTrialsPerMinigame);
                    trialsRemaining = trialsTotal;
                    timeBetweenRounds = MinigameManager.Instance.globalTrialGap;
                }

                MinigameManager.Instance.NotifyMinigameStarted(MinigameType.SimonSays);
                runStarted = true;
                BeginStartBuffer();
            }
        }
        else
        {
            if (trialsRemaining > 0)
            {
                BeginStartBuffer();
            }

        }
    }


    private void BeginStartBuffer()
    {
        state = SimonState.StartBuffer;
        stateTimer = startBufferTime;

        if (panelRoot != null)
            panelRoot.SetActive(true);

        if (phaseText != null)
            phaseText.text = "GET READY";

        if (option1Text != null) option1Text.text = "";
        if (option2Text != null) option2Text.text = "";

        if (colorDisplay != null)
            colorDisplay.color = Color.black;

        if (feedbackText != null)
            feedbackText.enabled = false;
    }

    private void HandleStartBuffer()
    {
        int timeLeft = Mathf.CeilToInt(stateTimer);

        if (phaseText != null)
            phaseText.text = timeLeft.ToString();

        if (stateTimer <= 0f)
        {
            StartWatchPhase();
        }
    }

    private void StartWatchPhase()
    {
        state = SimonState.ShowingSequence;
        stateTimer = 0f;

        GenerateSequence();
        GenerateOptions();

        if (phaseText != null)
            phaseText.text = "WATCH THE ORDER";

        if (option1Text != null) option1Text.text = "";
        if (option2Text != null) option2Text.text = "";

        if (colorDisplay != null)
            colorDisplay.color = Color.black;

        if (feedbackText != null)
            feedbackText.enabled = false;

        if (sequenceCoroutine != null)
            StopCoroutine(sequenceCoroutine);
        sequenceCoroutine = StartCoroutine(PlaySequenceCoroutine());
    }

    private void StartAnswerPhase()
    {
        state = SimonState.WaitingForAnswer;
        stateTimer = answerDuration;

        currentTrialStartTime = Time.time;

        if (phaseText != null)
            phaseText.text = "NOW ANSWER";

        if (feedbackText != null)
            feedbackText.enabled = false;

        if (correctOptionIndex == 1)
        {
            if (option1Text != null)
                option1Text.text = $"Say/Press 1: {correctSequenceStr}";
            if (option2Text != null)
                option2Text.text = $"Say/Press 2: {wrongSequenceStr}";
        }
        else
        {
            if (option1Text != null)
                option1Text.text = $"Say/Press 1: {wrongSequenceStr}";
            if (option2Text != null)
                option2Text.text = $"Say/Press 2: {correctSequenceStr}";
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

        if (neverTimeout || MinigameManager.Instance == null ||
            MinigameManager.Instance.globalAnswerDuration <= 0f)
        {
            return;
        }

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

        Debug.Log($"[SimonGame] HandleNumberRecognized called with: {number}, state={state}");

        if (state == SimonState.WaitingForAnswer && (number == 1 || number == 2))
        {
            HandleAnswer(number);
        }
    }

    private void HandleUnrecognizedSpeech()
    {
        if (inputMode != InputMode.Microphone)
            return;

        if (state != SimonState.WaitingForAnswer)
            return;

        Debug.Log("[SimonGame] Unrecognized speech during answer phase – prompting repeat.");
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

    private void HandleAnswer(int chosen)
    {
        bool correct = (chosen == correctOptionIndex);
        MinigameOutcome outcome = correct ? MinigameOutcome.Correct : MinigameOutcome.Incorrect;

        int trialsDoneSoFar = trialsTotal - trialsRemaining + 1;
        if (trialsDoneSoFar > trialsTotal)
            trialsDoneSoFar = trialsTotal;

        string detail =
            $"[Trial {trialsDoneSoFar}/{trialsTotal}] " +
            $"Correct={correctSequenceStr}, Wrong={wrongSequenceStr}, PlayerPick={chosen}";

        float responseTime = Time.time - currentTrialStartTime;

        MinigameManager.Instance.RegisterResult(
            MinigameType.SimonSays,
            outcome,
            correct,
            detail,
            responseTime
        );

        EndRound();
    }

    private void Timeout()
    {
        int trialsDoneSoFar = trialsTotal - trialsRemaining + 1;
        if (trialsDoneSoFar > trialsTotal)
            trialsDoneSoFar = trialsTotal;

        string detail =
            $"[Trial {trialsDoneSoFar}/{trialsTotal}] " +
            $"Correct={correctSequenceStr} (Timeout)";

        float responseTime = Time.time - currentTrialStartTime;

        MinigameManager.Instance.RegisterResult(
            MinigameType.SimonSays,
            MinigameOutcome.Timeout,
            false,
            detail,
            responseTime
        );

        EndRound();
    }

    private void EndRound()
    {
        state = SimonState.Cooldown;
        stateTimer = timeBetweenRounds;

        if (panelRoot != null)
            panelRoot.SetActive(false);

        if (colorDisplay != null)
            colorDisplay.color = Color.black;

        if (feedbackText != null)
            feedbackText.enabled = false;

        if (sequenceCoroutine != null)
        {
            StopCoroutine(sequenceCoroutine);
            sequenceCoroutine = null;
        }

        trialsRemaining--;

        if (trialsRemaining <= 0)
        {
            runStarted = false;

            if (MinigameManager.Instance != null)
                MinigameManager.Instance.NotifyMinigameEnded();
        }
    }


    private void GenerateSequence()
    {
        int len = 3;
        sequence = new int[len];

        for (int i = 0; i < len; i++)
        {
            sequence[i] = Random.Range(0, 3);
        }

        correctSequenceStr = SequenceToString(sequence);
    }

    private void GenerateOptions()
    {
        int[] wrong = (int[])sequence.Clone();

        int indexToChange = Random.Range(0, wrong.Length);

        int original = wrong[indexToChange];
        int newVal = Random.Range(0, 3);

        if (newVal == original)
        {
            newVal = (newVal + 1) % 3;
        }

        wrong[indexToChange] = newVal;

        wrongSequenceStr = SequenceToString(wrong);

        correctOptionIndex = Random.Range(1, 3);
    }

    private string SequenceToString(int[] seq)
    {
        string s = "";
        for (int i = 0; i < seq.Length; i++)
        {
            s += colorWords[seq[i]];
            if (i < seq.Length - 1)
                s += " ";
        }
        return s;
    }

    private IEnumerator PlaySequenceCoroutine()
    {
        for (int i = 0; i < sequence.Length; i++)
        {
            if (colorDisplay != null)
                colorDisplay.color = colorValues[sequence[i]];

            yield return new WaitForSeconds(colorShowDuration);

            if (colorDisplay != null)
                colorDisplay.color = Color.black;

            yield return new WaitForSeconds(colorGapDuration);
        }

        StartAnswerPhase();
    }
}
