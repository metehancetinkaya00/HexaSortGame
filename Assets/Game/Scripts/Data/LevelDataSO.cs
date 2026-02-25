using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "HexaSort/Level Data", fileName = "LevelData")]
public class LevelDataSO : ScriptableObject
{
    public List<SetData> sets = new();
}

[Serializable]
public class SetData
{
    public string setName = "Set 1";

   
    public List<PieceData> pieces = new();
}

[Serializable]
public class PieceData
{
    
    public List<TileColor> tiles = new();
}
