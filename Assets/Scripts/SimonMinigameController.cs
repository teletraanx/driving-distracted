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

    // Microphone manager reference
    private MicrophoneManager microphoneManager;

    private void Start()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);

        if (colorDisplay != null)
            colorDisplay.color = Color.black;

        state = SimonState.Cooldown;
        stateTimer = timeBetweenRounds;
        
        // Get reference from singleton
        if (MicrophoneManagerSingleton.Instance != null)
        {
            microphoneManager = MicrophoneManagerSingleton.Instance.GetMicrophoneManager();
            microphoneManager.OnNumberRecognized += HandleNumberRecognized;
        }
    }

    private void OnDestroy()
    {
        // Clean up event subscription
        if (microphoneManager != null)
            microphoneManager.OnNumberRecognized -= HandleNumberRecognized;
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
        if (stateTimer <= 0f &&
            MinigameManager.Instance != null &&
            MinigameManager.Instance.CanStartMinigame(MinigameType.SimonSays))
        {
            MinigameManager.Instance.NotifyMinigameStarted(MinigameType.SimonSays);
            BeginStartBuffer();
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

        option1Text.text = "";
        option2Text.text = "";

        colorDisplay.color = Color.black;
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

        option1Text.text = "";
        option2Text.text = "";
        colorDisplay.color = Color.black;

        if (sequenceCoroutine != null)
            StopCoroutine(sequenceCoroutine);
        sequenceCoroutine = StartCoroutine(PlaySequenceCoroutine());
    }

    private void StartAnswerPhase()
    {
        state = SimonState.WaitingForAnswer;
        stateTimer = answerDuration;

        if (phaseText != null)
            phaseText.text = "NOW ANSWER";

        if (correctOptionIndex == 1)
        {
            option1Text.text = $"Press 1: {correctSequenceStr}";
            option2Text.text = $"Press 2: {wrongSequenceStr}";
        }
        else
        {
            option1Text.text = $"Press 1: {wrongSequenceStr}";
            option2Text.text = $"Press 2: {correctSequenceStr}";
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

        if (stateTimer <= 0f)
        {
            Timeout();
        }
    }

    private void HandleNumberRecognized(int number)
    {
        // Only process if we're in the answer waiting state and number is valid
        if (state == SimonState.WaitingForAnswer && (number == 1 || number == 2))
        {
            HandleAnswer(number);
        }
    }

    private int GetPlayerInput()
    {
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

        string detail = $"Correct={correctSequenceStr}, Wrong={wrongSequenceStr}, PlayerPick={chosen}";

        MinigameManager.Instance.RegisterResult(
            MinigameType.SimonSays,
            outcome,
            correct,
            detail
        );

        EndRound();
    }

    private void Timeout()
    {
        string detail = $"Correct={correctSequenceStr} (Timeout)";

        MinigameManager.Instance.RegisterResult(
            MinigameType.SimonSays,
            MinigameOutcome.Timeout,
            false,
            detail
        );

        EndRound();
    }

    private void EndRound()
    {
        state = SimonState.Cooldown;
        stateTimer = timeBetweenRounds;

        if (panelRoot != null)
            panelRoot.SetActive(false);

        colorDisplay.color = Color.black;

        if (sequenceCoroutine != null)
        {
            StopCoroutine(sequenceCoroutine);
            sequenceCoroutine = null;
        }

        MinigameManager.Instance.NotifyMinigameEnded();
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

    private System.Collections.IEnumerator PlaySequenceCoroutine()
    {
        for (int i = 0; i < sequence.Length; i++)
        {
            colorDisplay.color = colorValues[sequence[i]];
            yield return new WaitForSeconds(colorShowDuration);

            colorDisplay.color = Color.black;
            yield return new WaitForSeconds(colorGapDuration);
        }

        StartAnswerPhase();
    }
}
