using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class StroopMinigameController : MonoBehaviour
{
    [Header("UI")]
    public GameObject panelRoot;
    public TMP_Text wordText;
    public TMP_Text instructionText;

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

    // Microphone manager reference
    private MicrophoneManager microphoneManager;

    private void Start()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);

        cooldownTimer = timeBetweenRounds;
        
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
        if (!questionActive)
        {
            cooldownTimer -= Time.deltaTime;

            if (cooldownTimer <= 0f &&
                MinigameManager.Instance != null &&
                MinigameManager.Instance.CanStartMinigame(MinigameType.Stroop))
            {
                MinigameManager.Instance.NotifyMinigameStarted(MinigameType.Stroop);
                StartQuestion();
            }
        }
        else
        {
            questionTimer -= Time.deltaTime;

            int input = GetPlayerInput();
            if (input != 0)
            {
                HandleAnswer(input);
                return;
            }

            if (questionTimer <= 0f)
            {
                Timeout();
            }
        }
    }

    private void HandleNumberRecognized(int number)
    {
        // Only process if we're in the question active state and number is valid
        if (questionActive && (number == 1 || number == 2))
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

    private void StartQuestion()
    {
        questionActive = true;
        questionTimer = questionDuration;

        if (panelRoot != null)
            panelRoot.SetActive(true);

        GenerateTrial();
        UpdateUI();
    }

    private void EndQuestion()
    {
        questionActive = false;
        cooldownTimer = timeBetweenRounds;

        if (panelRoot != null)
            panelRoot.SetActive(false);

        if (MinigameManager.Instance != null)
            MinigameManager.Instance.NotifyMinigameEnded();
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
            instructionText.text = "Does the WORD match the COLOR?\nPress 1 = YES\nPress 2 = NO";
        }
    }

    private void HandleAnswer(int input)
    {
        bool playerSaysMatch = (input == 1);
        bool correct = (playerSaysMatch == isMatch);

        MinigameOutcome outcome = correct ? MinigameOutcome.Correct : MinigameOutcome.Incorrect;
        string detail = $"Stroop: Word={colorWords[meaningIndex]}, Color={colorWords[colorIndex]}, IsMatch={isMatch}, PlayerSaysMatch={playerSaysMatch}";

        if (MinigameManager.Instance != null)
        {
            MinigameManager.Instance.RegisterResult(
                MinigameType.Stroop,
                outcome,
                correct,
                detail
            );
        }

        EndQuestion();
    }

    private void Timeout()
    {
        string detail = $"Stroop: Word={colorWords[meaningIndex]}, Color={colorWords[colorIndex]} (Timeout)";

        if (MinigameManager.Instance != null)
        {
            MinigameManager.Instance.RegisterResult(
                MinigameType.Stroop,
                MinigameOutcome.Timeout,
                false,
                detail
            );
        }

        EndQuestion();
    }
}
