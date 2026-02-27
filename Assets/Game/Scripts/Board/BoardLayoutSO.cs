using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Hexasort/Board Layout", fileName = "BoardLayout")]
public class BoardLayoutSO : ScriptableObject
{
    public List<HexCoordList> cells = new();

    public bool Contains(int q, int r)
    {
        if (cells == null)
        {
            return false;
        }

        for (int i = 0; i < cells.Count; i++)
        {
            var row = cells[i];
            if (row == null || row.items == null)
            {
                continue;
            }

            for (int j = 0; j < row.items.Count; j++)
            {
                Hex h = row.items[j].ToHex();
                if (h.col == q && h.row == r)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public IEnumerable<Hex> EnumerateHexes()
    {
        if (cells == null)
        {
            yield break;
        }

        for (int i = 0; i < cells.Count; i++)
        {
            var row = cells[i];
            if (row == null || row.items == null)
            {
                continue;
            }

            for (int j = 0; j < row.items.Count; j++)
            {
                yield return row.items[j].ToHex();
            }
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

    public Hex ToHex()
    {
        return new Hex(Mathf.RoundToInt(q), Mathf.RoundToInt(r));
    }
}