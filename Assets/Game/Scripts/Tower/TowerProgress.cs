using UnityEngine;

public static class TowerProgress
{
    private const string TotalTowerStepKey = "TotalTowerStep";
    private const string PendingTowerStepKey = "PendingTowerStep";

    public static int TotalTowerStep
    {
        get
        {
            return PlayerPrefs.GetInt(TotalTowerStepKey, 0);
        }
        private set
        {
            int safeValue = Mathf.Max(0, value);
            PlayerPrefs.SetInt(TotalTowerStepKey, safeValue);
            PlayerPrefs.Save();
        }
    }

    public static int PendingTowerStep
    {
        get
        {
            return PlayerPrefs.GetInt(PendingTowerStepKey, 0);
        }
        private set
        {
            int safeValue = Mathf.Max(0, value);
            PlayerPrefs.SetInt(PendingTowerStepKey, safeValue);
            PlayerPrefs.Save();
        }
    }

    public static void AddSteps(int stepCount)
    {
        if (stepCount <= 0)
        {
            return;
        }

        TotalTowerStep += stepCount;
        PendingTowerStep += stepCount;
    }

    public static bool HasPendingStep()
    {
        return PendingTowerStep > 0;
    }

    public static bool ConsumeOnePendingStep()
    {
        if (PendingTowerStep <= 0)
        {
            return false;
        }

        PendingTowerStep -= 1;
        return true;
    }

    public static int GetAppliedStepCount()
    {
        int value = TotalTowerStep - PendingTowerStep;
        if (value < 0)
        {
            value = 0;
        }

        return value;
    }

    public static void ResetTowerProgress()
    {
        TotalTowerStep = 0;
        PendingTowerStep = 0;
    }
}