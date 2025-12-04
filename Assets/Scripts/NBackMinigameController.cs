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

    [Header("Timing")]
    public float timeBetweenRounds = 0f;   
    public float startBufferTime = 3f;     
    public float letterShowDuration = 1f;
    public float letterGapDuration = 0.5f;
    public float answerDuration = 8f;

    [Header("Sequence Settings")]
    public int sequenceLength = 3;         

    private NBackState state = NBackState.Cooldown;
    private float stateTimer = 0f;

    private char[] sequence;
    private char correctLetter;
    private char wrongLetter;
    private int correctOptionIndex; 

    private char[] possibleLetters = { 'A', 'B', 'C', 'D' };

    private Coroutine sequenceCoroutine;

    private void Start()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);

        state = NBackState.Cooldown;
        stateTimer = timeBetweenRounds;
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
        if (stateTimer <= 0f &&
            MinigameManager.Instance != null &&
            MinigameManager.Instance.CanStartMinigame(MinigameType.NBack))
        {
            MinigameManager.Instance.NotifyMinigameStarted(MinigameType.NBack);
            BeginStartBuffer();
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

        if (sequenceCoroutine != null)
            StopCoroutine(sequenceCoroutine);
        sequenceCoroutine = StartCoroutine(PlaySequenceCoroutine());
    }

    private void StartAnswerPhase()
    {
        state = NBackState.WaitingForAnswer;
        stateTimer = answerDuration;

        if (phaseText != null)
            phaseText.text = "NOW ANSWER";

        if (instructionText != null)
            instructionText.text = "Which letter came BEFORE the last one?";

        if (letterText != null)
            letterText.text = ""; 

        if (correctOptionIndex == 1)
        {
            if (option1Text != null)
                option1Text.text = $"Press 1: {correctLetter}";
            if (option2Text != null)
                option2Text.text = $"Press 2: {wrongLetter}";
        }
        else
        {
            if (option1Text != null)
                option1Text.text = $"Press 1: {wrongLetter}";
            if (option2Text != null)
                option2Text.text = $"Press 2: {correctLetter}";
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

        if (sequenceCoroutine != null)
        {
            StopCoroutine(sequenceCoroutine);
            sequenceCoroutine = null;
        }

        if (MinigameManager.Instance != null)
            MinigameManager.Instance.NotifyMinigameEnded();
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

    private void HandleAnswer(int chosenOption)
    {
        bool isCorrect = (chosenOption == correctOptionIndex);

        MinigameOutcome outcome = isCorrect ? MinigameOutcome.Correct : MinigameOutcome.Incorrect;

        string detail =
            $"NBack: sequence={new string(sequence)}, " +
            $"correctLetter={correctLetter}, wrongLetter={wrongLetter}, playerPick={chosenOption}";

        if (MinigameManager.Instance != null)
        {
            MinigameManager.Instance.RegisterResult(
                MinigameType.NBack,
                outcome,
                isCorrect,
                detail
            );
        }

        EndRound();
    }

    private void Timeout()
    {
        string detail = $"NBack: sequence={new string(sequence)} (Timeout)";

        if (MinigameManager.Instance != null)
        {
            MinigameManager.Instance.RegisterResult(
                MinigameType.NBack,
                MinigameOutcome.Timeout,
                false,
                detail
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

    private System.Collections.IEnumerator PlaySequenceCoroutine()
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
