using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Hexasort/Board Layout", fileName = "BoardLayout")]
public class BoardLayoutSO : ScriptableObject
{
    [Tooltip("Each element is a row/group. Each row has its own list of axial coords (q,r).")]
    public List<HexCoordList> cells = new();

    public bool Contains(int q, int r)
    {
        for (int i = 0; i < cells.Count; i++)
        {
            var row = cells[i];
            if (row == null || row.items == null) continue;

            for (int j = 0; j < row.items.Count; j++)
            {
                if ((int)row.items[j].q == q && (int)row.items[j].r == r)
                    return true;
            }
        }
        return false;
    }

    // İstersen HexBoard için düz liste verelim
    public IEnumerable<Hex> EnumerateHexes()
    {
        for (int i = 0; i < cells.Count; i++)
        {
            var row = cells[i];
            if (row == null || row.items == null) continue;

            for (int j = 0; j < row.items.Count; j++)
                yield return row.items[j].ToHex();
        }
    }
}

[System.Serializable]
public class HexCoordList
{
    public List<HexCoord> items = new();
}

[System.Serializable]
public struct HexCoord
{
    public float q;
    public float r;

    public HexCoord(float q, float r)
    {
        this.q = q;
        this.r = r;
    }

    public Hex ToHex() => new Hex(q, r);
}
