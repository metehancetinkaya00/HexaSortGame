using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneFlow : MonoBehaviour
{
    public string mainMenuSceneName = "MainMenu";
    public string levelSceneName = "LevelScene";

    public LevelController levelController;

    public void PlayNewGame()
    {
        GameProgress.ResetProgress();
        TowerProgress.ResetTowerProgress();
        SceneManager.LoadScene(levelSceneName);
    }

    public void ContinueGame()
    {
        SceneManager.LoadScene(levelSceneName);
    }

    public void BackToMenu()
    {
        SceneManager.LoadScene(mainMenuSceneName);
    }

    public void NextLevelOrMenu()
    {
        if (levelController != null)
        {
            if (levelController.HasNextLevel())
            {
                levelController.LoadNextLevel();
                return;
            }
        }

        SceneManager.LoadScene(mainMenuSceneName);
    }

    public void RetryLevel()
    {
        if (levelController != null)
        {
            levelController.RetryCurrentLevel();
            return;
        }

        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}