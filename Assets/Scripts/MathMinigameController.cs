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

    [Header("Timing")]
    public float timeBetweenQuestions = 5f;  
    public float questionDuration = 8f;      

    private float cooldownTimer = 0f;
    private float questionTimer = 0f;
    private bool questionActive = false;

    private int correctOptionIndex;      
    private int correctAnswer;
    private int option1Value;
    private int option2Value;
    private string currentQuestion;    

    // Microphone manager reference
    private MicrophoneManager microphoneManager;

    private void Start()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);

        cooldownTimer = timeBetweenQuestions;
        
        if (MicrophoneManagerSingleton.Instance != null)
        {
            Debug.Log("[MathGame] Starting MicrophoneManager instance...");
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
                MinigameManager.Instance.CanStartMinigame(MinigameType.Math))
            {
                MinigameManager.Instance.NotifyMinigameStarted(MinigameType.Math);
                StartNewQuestion(); 
            }
        }
        else
        {
            questionTimer -= Time.deltaTime;

            int chosenIndex = GetAnswerInput();
            if (chosenIndex != 0)
            {
                HandleAnswer(chosenIndex);
            }
            else if (questionTimer <= 0f)
            {
                HandleTimeout();
            }
        }
    }

    private int GetAnswerInput()
    {
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
        // Only process if question is active and number is valid
        if (questionActive && (number == 1 || number == 2))
        {
            HandleAnswer(number);
        }
    }

    private void StartNewQuestion()
    {
        questionActive = true;
        questionTimer = questionDuration;

        if (panelRoot != null)
            panelRoot.SetActive(true);

        GenerateQuestion();
        UpdateUI();
    }

    private void EndQuestion()
    {
        questionActive = false;
        cooldownTimer = timeBetweenQuestions;

        if (panelRoot != null)
            panelRoot.SetActive(false);

        if (MinigameManager.Instance != null)
            MinigameManager.Instance.NotifyMinigameEnded();
    }

    private void HandleAnswer(int chosenIndex)
    {
        bool isCorrect = (chosenIndex == correctOptionIndex);
        var outcome = isCorrect ? MinigameOutcome.Correct : MinigameOutcome.Incorrect;

        string chosenValue = chosenIndex == 1 ? option1Value.ToString() : option2Value.ToString();
        string detail = $"{currentQuestion}  Correct={correctAnswer}, Chosen={chosenValue}";

        if (MinigameManager.Instance != null)
        {
            MinigameManager.Instance.RegisterResult(
                MinigameType.Math,
                outcome,
                isCorrect,
                detail
            );
        }

        EndQuestion();
    }

    private void HandleTimeout()
    {
        string detail = $"{currentQuestion}  TIMED OUT (Correct={correctAnswer})";

        if (MinigameManager.Instance != null)
        {
            MinigameManager.Instance.RegisterResult(
                MinigameType.Math,
                MinigameOutcome.Timeout,
                false,
                detail
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
                opChar = '�';
                correctAnswer = a * b;
                break;
        }

        currentQuestion = $"{a} {opChar} {b} = ?";

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
            option1Text.text = $"Press 1: {option1Value}";

        if (option2Text != null)
            option2Text.text = $"Press 2: {option2Value}";
    }
}
