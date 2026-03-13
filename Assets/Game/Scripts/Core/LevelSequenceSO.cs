using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Hexasort/Level Sequence", fileName = "LevelSequence")]
public class LevelSequenceSO : ScriptableObject
{
    public List<LevelEntry> levels = new List<LevelEntry>();

    public int Count
    {
        get
        {
            if (levels == null)
            {
                return 0;
            }

            return levels.Count;
        }
    }

    public bool HasLevel(int index)
    {
        return index >= 0 && index < Count;
    }

    public LevelEntry GetLevel(int index)
    {
        if (!HasLevel(index))
        {
            return null;
        }

        return levels[index];
    }
}

[Serializable]
public class LevelEntry
{
    public string levelName = "Level";

    public HexLevelLayout layout;
    public RandomPackConfigSO randomPack;

    public int targetScore = 50;

    public int randomSeed = 0;
    public bool chooseRandomAnchorEachPack = true;
}