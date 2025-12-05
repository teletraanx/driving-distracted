using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class EndScreenController : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text resultsText;
    public TMP_Text infoText;

    private void Start()
    {
        if (infoText != null)
            infoText.text = "";

        BuildResultsDisplay();
    }

    private void BuildResultsDisplay()
    {
        if (resultsText == null)
            return;

        if (MinigameManager.Instance == null || MinigameManager.Instance.results == null)
        {
            resultsText.text = "No results found.";
            return;
        }

        var mgr = MinigameManager.Instance;
        var sb = new StringBuilder();

        sb.AppendLine("Run Results");
        sb.AppendLine("-----------");

        foreach (var r in mgr.results)
        {
            string timePart = (r.responseTimeSeconds >= 0f)
                ? $"{r.responseTimeSeconds:0.00}s"
                : "n/a";

            sb.AppendLine(
                $"{r.type} - {r.outcome} - " +
                $"Correct={r.wasCorrect} - " +
                $"Time={timePart} - " +
                $"{r.detail}"
            );
        }

        sb.AppendLine();
        sb.AppendLine("Collision Stats");
        sb.AppendLine("---------------");
        sb.AppendLine($"Total collisions: {mgr.totalCarCollisions}");
        sb.AppendLine($"While no minigame: {mgr.collisionsNoMinigame}");

        if (mgr.collisionsByMinigame != null)
        {
            foreach (var s in mgr.collisionsByMinigame)
            {
                sb.AppendLine($"{s.type}: {s.collisionCount}");
            }
        }

        resultsText.text = sb.ToString();
    }

    public void OnSaveAndOpenPressed()
    {
        if (MinigameManager.Instance == null)
        {
            if (infoText != null)
                infoText.text = "Cannot save: no MinigameManager.";
            return;
        }

        string folder = Application.persistentDataPath;
        string filename = $"minigame_results_{System.DateTime.Now:yyyyMMdd_HHmmss}.txt";
        string fullPath = Path.Combine(folder, filename);

        try
        {
            var mgr = MinigameManager.Instance;
            var sb = new StringBuilder();

            sb.AppendLine("Run Results");
            sb.AppendLine("-----------");

            foreach (var r in mgr.results)
            {
                string timePart = (r.responseTimeSeconds >= 0f)
                    ? $"{r.responseTimeSeconds:0.00}s"
                    : "n/a";

                sb.AppendLine(
                    $"{r.type} - {r.outcome} - " +
                    $"Correct={r.wasCorrect} - " +
                    $"Time={timePart} - " +
                    $"{r.detail}"
                );
            }

            sb.AppendLine();
            sb.AppendLine("Collision Stats");
            sb.AppendLine("---------------");
            sb.AppendLine($"Total collisions: {mgr.totalCarCollisions}");
            sb.AppendLine($"While no minigame: {mgr.collisionsNoMinigame}");

            if (mgr.collisionsByMinigame != null)
            {
                foreach (var s in mgr.collisionsByMinigame)
                {
                    sb.AppendLine($"{s.type}: {s.collisionCount}");
                }
            }

            File.WriteAllText(fullPath, sb.ToString());

            if (infoText != null)
                infoText.text = $"Saved to: {fullPath}";

            Application.OpenURL("file://" + fullPath);
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Failed to save results: " + ex);
            if (infoText != null)
                infoText.text = "Failed to save results.";
        }
    }

    public void OnBackToMenuPressed()
    {
        if (MinigameManager.Instance != null)
        {
            MinigameManager.Instance.ResetRunState();
            MinigameManager.Instance.results.Clear();
        }

        if (MinigameManager.Instance != null)
        {
            SceneManager.LoadScene(MinigameManager.Instance.mainMenuSceneName);
        }
        else
        {
            SceneManager.LoadScene("MainMenu");
        }
    }
}
