using UnityEngine;
using UnityEngine.SceneManagement;

public class GameUIController : MonoBehaviour
{
    public void OnExitToMenuPressed()
    {

        // Stop speech recognition when "To Menu" button is pressed (NOT WORKING)
        /*if (MicrophoneManagerSingleton.Instance != null)
        {
            var microphoneManager = MicrophoneManagerSingleton.Instance.GetMicrophoneManager();
            if (microphoneManager != null)
            {
                microphoneManager.StopMicrophoneStreaming();
            }
        }*/

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
