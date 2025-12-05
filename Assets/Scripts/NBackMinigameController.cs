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
    public int sequenceLength = 4;        
    public int nBackOffset = 2;           

    [Header("Timeout")]
    public bool neverTimeout = true;

    private NBackState state = NBackState.Cooldown;
    private float stateTimer = 0f;

    private char[] sequence;
    private char probeLetter;          
    private bool isMatchTarget;        

    private char[] possibleLetters = { 'A', 'B', 'C', 'D' };

    private Coroutine sequenceCoroutine;
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

        state = NBackState.Cooldown;

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

        int minLen = Mathf.Max(2, nBackOffset + 1);
        sequenceLength = Mathf.Max(sequenceLength, minLen);

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
            case NBackState.Cooldown:
                HandleCooldown();
                break;

            case NBackState.StartBuffer:
                HandleStartBuffer();
                break;

            case NBackState.ShowingSequence:              
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
                if (MinigameManager.Instance != null)
                {
                    trialsTotal = Mathf.Max(1, MinigameManager.Instance.globalTrialsPerMinigame);
                    trialsRemaining = trialsTotal;
                    timeBetweenRounds = MinigameManager.Instance.globalTrialGap;
                }

                MinigameManager.Instance.NotifyMinigameStarted(MinigameType.NBack);
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

        GenerateSequenceAndProbe();  

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

    private void StartAnswerPhase()
    {
        state = NBackState.WaitingForAnswer;
        stateTimer = answerDuration;

        currentTrialStartTime = Time.time;

        if (phaseText != null)
            phaseText.text = "NOW ANSWER";

        if (instructionText != null)
        {
            instructionText.text =
                $"Did this letter match the letter {nBackOffset} back?\n" +
                "Say/Press 1: YES\n" +
                "Say/Press 2: NO";
        }

        if (letterText != null)
            letterText.text = probeLetter.ToString();

        if (feedbackText != null)
            feedbackText.enabled = false;

        if (option1Text != null)
            option1Text.text = "Say/Press 1: YES";

        if (option2Text != null)
            option2Text.text = "Say/Press 2: NO";
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
            return;

        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f)
        {
            Timeout();
        }
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

    private void HandleAnswer(int chosenOption)
    {
        bool playerSaysMatch;
        if (chosenOption == 1)      
            playerSaysMatch = true;
        else if (chosenOption == 2) 
            playerSaysMatch = false;
        else
            return;

        bool isCorrect = (playerSaysMatch == isMatchTarget);
        MinigameOutcome outcome = isCorrect ? MinigameOutcome.Correct : MinigameOutcome.Incorrect;

        int trialsDoneSoFar = trialsTotal - trialsRemaining + 1;
        if (trialsDoneSoFar > trialsTotal)
            trialsDoneSoFar = trialsTotal;

        string seqString = new string(sequence);
        string detail =
            $"[Trial {trialsDoneSoFar}/{trialsTotal}] NBack (N={nBackOffset}): " +
            $"sequence={seqString}, probe={probeLetter}, " +
            $"isMatchTarget={isMatchTarget}, playerSaysMatch={playerSaysMatch}";

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

        string seqString = new string(sequence);
        string detail =
            $"[Trial {trialsDoneSoFar}/{trialsTotal}] NBack (N={nBackOffset}): " +
            $"sequence={seqString}, probe={probeLetter}, isMatchTarget={isMatchTarget} (Timeout)";

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

        trialsRemaining--;

        if (trialsRemaining <= 0)
        {
            runStarted = false;

            if (MinigameManager.Instance != null)
                MinigameManager.Instance.NotifyMinigameEnded();
        }
    }

    private void GenerateSequenceAndProbe()
    {
        int minLen = Mathf.Max(2, nBackOffset + 1);
        sequenceLength = Mathf.Max(sequenceLength, minLen);

        sequence = new char[sequenceLength];

        for (int i = 0; i < sequenceLength; i++)
        {
            sequence[i] = possibleLetters[Random.Range(0, possibleLetters.Length)];
        }

        isMatchTarget = (Random.value < 0.5f);

        int nBackIndex = sequenceLength - nBackOffset;
        nBackIndex = Mathf.Clamp(nBackIndex, 0, sequenceLength - 1);

        if (isMatchTarget)
        {
            probeLetter = sequence[nBackIndex];
        }
        else
        {
            probeLetter = sequence[nBackIndex];
            while (probeLetter == sequence[nBackIndex])
            {
                probeLetter = possibleLetters[Random.Range(0, possibleLetters.Length)];
            }
        }
    }
}
