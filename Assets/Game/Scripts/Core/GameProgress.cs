public static class GameProgress
{
    public static int CurrentLevelIndex = 0;

    public static void ResetProgress()
    {
        CurrentLevelIndex = 0;
    }

    public static void SetLevel(int levelIndex)
    {
        if (levelIndex < 0)
        {
            levelIndex = 0;
        }

        CurrentLevelIndex = levelIndex;
    }

    public static void NextLevel()
    {
        CurrentLevelIndex++;
    }
}