using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class MinigameMenuController : MonoBehaviour
{
    [Header("UI References")]
    public TMP_Dropdown countDropdown;
    public Transform dropdownParent;
    public TMP_Dropdown dropdownPrefab;
    public TMP_Text warningText;

    [Header("Timing UI")] 
    public TMP_InputField answerTimeInputField;
    public TMP_InputField gapTimeInputField;

    [Header("Game Settings")]
    public string gameplaySceneName = "MainGame"; 
    public int minCount = 1;
    public int maxCount = 12;

    private readonly List<TMP_Dropdown> dropdowns = new List<TMP_Dropdown>();

    private readonly List<(string label, MinigameType type)> minigameOptions =
        new List<(string, MinigameType)>
        {
            ("Math",       MinigameType.Math),
            ("Simon Says", MinigameType.SimonSays),
            ("Stroop",     MinigameType.Stroop),
            ("N-Back",     MinigameType.NBack)
        };

    private void Start()
    {
        if (warningText != null)
            warningText.text = "";

        SetupCountDropdown();
        SetupTimingFields(); 
    }

    private void SetupCountDropdown()
    {
        if (countDropdown == null)
        {
            Debug.LogWarning("CountDropdown is not assigned.");
            return;
        }

        countDropdown.ClearOptions();

        List<TMP_Dropdown.OptionData> options = new List<TMP_Dropdown.OptionData>();

        for (int i = minCount; i <= maxCount; i++)
        {
            options.Add(new TMP_Dropdown.OptionData(i.ToString()));
        }

        countDropdown.AddOptions(options);

        int defaultCount = 4;
        int defaultIndex = Mathf.Clamp(defaultCount, minCount, maxCount) - minCount;
        countDropdown.value = defaultIndex;
        countDropdown.RefreshShownValue();

        int initialCount = defaultIndex + minCount;
        RebuildDropdowns(initialCount);
    }

    private void SetupTimingFields()
    {
        float defaultAnswer = 8f;
        float defaultGap = 5f;

        if (MinigameManager.Instance != null)
        {
            defaultAnswer = MinigameManager.Instance.globalAnswerDuration;
            defaultGap = MinigameManager.Instance.globalMinigameGap;
        }

        if (answerTimeInputField != null)
            answerTimeInputField.text = defaultAnswer.ToString("0");

        if (gapTimeInputField != null)
            gapTimeInputField.text = defaultGap.ToString("0");
    }

    public void OnCountDropdownChanged(int optionIndex)
    {
        int count = optionIndex + minCount;

        if (warningText != null)
            warningText.text = "";

        RebuildDropdowns(count);
    }

    private void RebuildDropdowns(int count)
    {
        foreach (var dd in dropdowns)
        {
            if (dd != null)
                Destroy(dd.gameObject);
        }
        dropdowns.Clear();

        for (int i = 0; i < count; i++)
        {
            TMP_Dropdown dd = Instantiate(dropdownPrefab, dropdownParent);
            dd.gameObject.name = $"MinigameDropdown_{i + 1}";

            dd.ClearOptions();

            List<TMP_Dropdown.OptionData> options = new List<TMP_Dropdown.OptionData>();
            foreach (var opt in minigameOptions)
            {
                options.Add(new TMP_Dropdown.OptionData(opt.label));
            }
            dd.AddOptions(options);

            dd.value = i % minigameOptions.Count;
            dd.RefreshShownValue();

            dropdowns.Add(dd);
        }
    }

    public void OnStartButtonPressed()
    {
        if (MinigameManager.Instance == null)
        {
            if (warningText != null)
                warningText.text = "No MinigameManager found!";
            Debug.LogError("MinigameManager.Instance is null. Make sure it exists in the Main Menu scene.");
            return;
        }

        if (dropdowns.Count == 0)
        {
            if (warningText != null)
                warningText.text = "Please choose at least one minigame.";
            return;
        }

        float answerTime = 8f;
        float gapTime = 5f;

        if (answerTimeInputField != null &&
            float.TryParse(answerTimeInputField.text, out float parsedAnswer) &&
            parsedAnswer > 0.1f)
        {
            answerTime = parsedAnswer;
        }

        if (gapTimeInputField != null &&
            float.TryParse(gapTimeInputField.text, out float parsedGap) &&
            parsedGap >= 0f)
        {
            gapTime = parsedGap;
        }

        MinigameManager.Instance.globalAnswerDuration = answerTime;
        MinigameManager.Instance.globalMinigameGap = gapTime;

        MinigameType[] order = new MinigameType[dropdowns.Count];

        for (int i = 0; i < dropdowns.Count; i++)
        {
            int idx = dropdowns[i].value;
            if (idx < 0 || idx >= minigameOptions.Count)
                idx = 0;

            order[i] = minigameOptions[idx].type;
        }

        MinigameManager.Instance.sequenceOrder = order;

        MinigameManager.Instance.ResetRunState();

        if (warningText != null)
            warningText.text = "";

        SceneManager.LoadScene(gameplaySceneName);
    }

    public void OnExitApplicationPressed()
    {
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #else
        Application.Quit();
        #endif
    }
}
