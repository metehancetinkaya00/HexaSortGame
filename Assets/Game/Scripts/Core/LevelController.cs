using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelController : MonoBehaviour
{
    [Header("References")]
    public LevelSequenceSO levelSequence;
    public HexBoard hexBoard;
    public ScoreManager scoreManager;

    [Header("Scene Names")]
    public string mainMenuSceneName = "MainMenu";

    private void Start()
    {
        ApplyCurrentLevel();
    }

    public void ApplyCurrentLevel()
    {
        if (levelSequence == null)
        {
            Debug.LogError("LevelSequence is missing.");
            return;
        }

        if (hexBoard == null)
        {
            Debug.LogError("HexBoard is missing.");
            return;
        }

        if (scoreManager == null)
        {
            Debug.LogError("ScoreManager is missing.");
            return;
        }

        int levelIndex = GameProgress.CurrentLevelIndex;

        if (!levelSequence.HasLevel(levelIndex))
        {
            GameProgress.SetLevel(0);
            levelIndex = 0;
        }

        LevelEntry level = levelSequence.GetLevel(levelIndex);
        if (level == null)
        {
            Debug.LogError("LevelEntry is missing.");
            return;
        }

        hexBoard.hexLevelLayout = level.layout;
        hexBoard.randomPack = level.randomPack;
        hexBoard.randomSeed = level.randomSeed;
        hexBoard.chooseRandomAnchorEachPack = level.chooseRandomAnchorEachPack;

        scoreManager.targetScore = level.targetScore;
        scoreManager.ResetScore();
        scoreManager.SetLevelNumber(levelIndex + 1);

        hexBoard.RestartLevel();
    }

    public void RetryCurrentLevel()
    {
        ApplyCurrentLevel();
    }

    public bool HasNextLevel()
    {
        if (levelSequence == null)
        {
            return false;
        }

        return levelSequence.HasLevel(GameProgress.CurrentLevelIndex + 1);
    }

    public void LoadNextLevel()
    {
        if (!HasNextLevel())
        {
            return;
        }

        GameProgress.NextLevel();
        ApplyCurrentLevel();
    }

    public void CompleteLevelAndReturnToMenu()
    {
        if (levelSequence == null)
        {
            SceneManager.LoadScene(mainMenuSceneName);
            return;
        }

        int levelIndex = GameProgress.CurrentLevelIndex;
        LevelEntry level = levelSequence.GetLevel(levelIndex);

        if (level != null)
        {
            TowerProgress.AddSteps(level.targetScore);
        }

        if (HasNextLevel())
        {
            GameProgress.NextLevel();
        }

        SceneManager.LoadScene(mainMenuSceneName);
    }
}