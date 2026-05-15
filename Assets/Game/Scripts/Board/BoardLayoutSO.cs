using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(menuName = "Hexasort/Board Layout", fileName = "BoardLayout")]
public class BoardLayoutSO : ScriptableObject
{
    public List<HexCoordList> cells = new();

    public bool Contains(int q, int r)
    {
        if (cells == null)
            return false;

        foreach (var row in cells)
        {
            if (row?.items == null)
                continue;

            foreach (var coord in row.items)
            {
                var h = coord.ToHex();
                if (h.col == q && h.row == r)
                    return true;
            }
        }

        return false;
    }

    public IEnumerable<Hex> EnumerateHexes()
    {
        if (cells == null)
            yield break;

        foreach (var row in cells)
        {
            if (row?.items == null)
                continue;

            foreach (var coord in row.items)
                yield return coord.ToHex();
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

    public Hex ToHex() => new Hex(Mathf.RoundToInt(q), Mathf.RoundToInt(r));
}