using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum MinigameType
{
    Math,
    SimonSays,
    Stroop,
    NBack
}

public enum MinigameOutcome
{
    Correct,
    Incorrect,
    Timeout
}

[System.Serializable]
public class MinigameResult
{
    public MinigameType type;
    public MinigameOutcome outcome;
    public string detail;
    public bool wasCorrect;
    public float responseTimeSeconds = -1f;
}

[System.Serializable]
public class MinigameCollisionStats
{
    public MinigameType type;
    public int collisionCount;
}

public class MinigameManager : MonoBehaviour
{
    public static MinigameManager Instance { get; private set; }

    private float currentMinigameStartTime = -1f;

    [Header("Results (debug)")]
    public List<MinigameResult> results = new List<MinigameResult>();

    [Header("Global Minigame Settings")]
    public float globalAnswerDuration = 8f;   
    public float globalMinigameGap = 5f;     

    [Header("Sequence Settings")]
    public MinigameType[] sequenceOrder = new MinigameType[]
    {
        MinigameType.Math,
        MinigameType.SimonSays,
        MinigameType.Stroop,
        MinigameType.NBack
    };

    [Header("Collision Stats")]
    public int totalCarCollisions = 0;
    public int collisionsNoMinigame = 0;
    public List<MinigameCollisionStats> collisionsByMinigame = new List<MinigameCollisionStats>();

    [Header("Run / Return Settings")]
    public string mainMenuSceneName = "MainMenu";  
    public string endScreenSceneName = "EndMenu"; 
    public float returnToMenuDelay = 5f;

    private bool isMinigameActive = false;
    private float nextAllowedStartTime = 0f;

    private int currentIndex = 0;    
    private int completedCount = 0;  

    private bool runFinished = false;
    private float runFinishedTime = 0f;

    private MinigameType activeMinigameType;   
    private bool hasActiveMinigame = false;


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        collisionsByMinigame.Clear();
        foreach (MinigameType t in System.Enum.GetValues(typeof(MinigameType)))
        {
            collisionsByMinigame.Add(new MinigameCollisionStats
            {
                type = t,
                collisionCount = 0
            });
        }
    }

    private void Update()
    {
        if (runFinished && Time.time - runFinishedTime >= returnToMenuDelay)
        {
            runFinished = false;
            SceneManager.LoadScene(endScreenSceneName);
        }
    }

    public void RegisterResult(
        MinigameType type,
        MinigameOutcome outcome,
        bool wasCorrect,
        string detail)
    {
        float responseTime = -1f;

        if (currentMinigameStartTime > 0f)
            responseTime = Time.time - currentMinigameStartTime;

        RegisterResult(type, outcome, wasCorrect, detail, responseTime);
    }

    public void RegisterResult(
        MinigameType type,
        MinigameOutcome outcome,
        bool wasCorrect,
        string detail,
        float responseTimeSeconds)
    {
        var r = new MinigameResult
        {
            type = type,
            outcome = outcome,
            wasCorrect = wasCorrect,
            detail = detail,
            responseTimeSeconds = responseTimeSeconds
        };

        results.Add(r);
        Debug.Log($"[Minigame] {type} - {outcome} - {detail} (time={responseTimeSeconds:0.00}s)");
    }

    public bool CanStartMinigame(MinigameType requester)
    {
        if (runFinished) return false;
        if (isMinigameActive) return false;
        if (Time.time < nextAllowedStartTime) return false;

        if (sequenceOrder == null || sequenceOrder.Length == 0)
            return true; 

        if (completedCount >= sequenceOrder.Length)
            return false;

        MinigameType expected = sequenceOrder[currentIndex];
        return requester == expected;
    }

    public void NotifyMinigameStarted(MinigameType type)
    {
        isMinigameActive = true;
        hasActiveMinigame = true;
        activeMinigameType = type;
        currentMinigameStartTime = Time.time;
    }

    public void NotifyMinigameEnded()
    {
        isMinigameActive = false;
        hasActiveMinigame = false;

        currentMinigameStartTime = -1f;

        nextAllowedStartTime = Time.time + globalMinigameGap;

        completedCount++;

        if (sequenceOrder != null && sequenceOrder.Length > 0)
        {
            currentIndex = Mathf.Min(completedCount, sequenceOrder.Length - 1);
        }

        if (sequenceOrder != null && completedCount >= sequenceOrder.Length)
        {
            runFinished = true;
            runFinishedTime = Time.time;
            Debug.Log("[Minigame] Run finished. Going to end screen...");

        }
    }

    public void ResetRunState()
    {
        isMinigameActive = false;
        nextAllowedStartTime = 0f;
        currentIndex = 0;
        completedCount = 0;
        runFinished = false;
        runFinishedTime = 0f;
    }

    public void RegisterCarCollision()
    {
        totalCarCollisions++;

        if (hasActiveMinigame)
        {
            foreach (var s in collisionsByMinigame)
            {
                if (s.type == activeMinigameType)
                {
                    s.collisionCount++;
                    return;
                }
            }
        }
        else
        {
            collisionsNoMinigame++;
        }
    }
}
