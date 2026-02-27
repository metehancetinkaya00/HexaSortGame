using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneManageScript : MonoBehaviour
{
    [Header("Next Scene")]
   
    public int nextSceneBuildIndex = 0;

    public void LoadNextScene()
    {
        if (nextSceneBuildIndex < 0 || nextSceneBuildIndex >= SceneManager.sceneCountInBuildSettings)
        {
            Debug.LogError($"Invalid scene index: {nextSceneBuildIndex}. Build Settings count: {SceneManager.sceneCountInBuildSettings}");
            return;
        }

        SceneManager.LoadScene(nextSceneBuildIndex);
    }

    public void ReloadScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}