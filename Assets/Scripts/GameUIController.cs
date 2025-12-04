using UnityEngine;
using UnityEngine.SceneManagement;

public class GameUIController : MonoBehaviour
{
    public void OnExitToMenuPressed()
    {
        if (MinigameManager.Instance != null)
        {
            MinigameManager.Instance.ResetRunState();
            MinigameManager.Instance.results.Clear();

            SceneManager.LoadScene(MinigameManager.Instance.mainMenuSceneName);
        }
        else
        {
            SceneManager.LoadScene("MainMenu");
        }
    }
}
