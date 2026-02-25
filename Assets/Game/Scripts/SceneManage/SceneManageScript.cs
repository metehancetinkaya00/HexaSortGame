using UnityEngine;
using UnityEngine.SceneManagement;
public class SceneManageScript:MonoBehaviour
{
    [Header("Next Scene")]
    [Tooltip("LoadNextScene() çaðrýlýnca gidilecek scene build index'i")]
    public int nextSceneBuildIndex = 0;

    public void LoadNextScene()
    {
       
        if (nextSceneBuildIndex < 0 || nextSceneBuildIndex >= SceneManager.sceneCountInBuildSettings)
        {
            Debug.LogError($"Invalid scene index: {nextSceneBuildIndex}. " +
                           $"Build Settings scene count: {SceneManager.sceneCountInBuildSettings}");
            return;
        }

        SceneManager.LoadScene(nextSceneBuildIndex);
    }

    public void ReloadScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
