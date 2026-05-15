using System.Collections.Generic;
using UnityEngine;

public enum HexGridOffsetMode
{
    OddR = 0,
    EvenR = 1
}

public enum HexCellKind
{
    Empty = 0,
    Normal = 1,
    Locked = 2
}

public struct HexLayoutCellInfo
{
    public Hex coord;
    public HexCellKind kind;
    public int requiredClearCount;
}

[CreateAssetMenu(menuName = "Hexasort/Hex Level Layout", fileName = "HexLevelLayout")]
public class HexLevelLayout : ScriptableObject
{
    public int width = 10;
    public int height = 10;

    public HexGridOffsetMode offsetMode = HexGridOffsetMode.OddR;

  
    public bool centerOnZero = true;
    public int centerOffsetX = 0;
    public int centerOffsetY = 0;

  
    public HexCellKind[] cellKinds;
    public int[] requiredClearCounts;

    public void EnsureCellsSize()
    {
        width = Mathf.Max(1, width);
        height = Mathf.Max(1, height);

        int targetSize = width * height;

        if (cellKinds == null)
            cellKinds = new HexCellKind[targetSize];

        if (requiredClearCounts == null)
            requiredClearCounts = new int[targetSize];

        if (cellKinds.Length != targetSize || requiredClearCounts.Length != targetSize)
            Resize(width, height);
    }

    public void Resize(int newWidth, int newHeight)
    {
        newWidth = Mathf.Max(1, newWidth);
        newHeight = Mathf.Max(1, newHeight);

        int oldWidth = width;
        int oldHeight = height;
        var oldKinds = cellKinds;
        var oldRequired = requiredClearCounts;

        width = newWidth;
        height = newHeight;

        cellKinds = new HexCellKind[width * height];
        requiredClearCounts = new int[width * height];

        if (oldKinds == null || oldRequired == null)
            return;

      
        int copyW = Mathf.Min(oldWidth, width);
        int copyH = Mathf.Min(oldHeight, height);

        for (int y = 0; y < copyH; y++)
        {
            for (int x = 0; x < copyW; x++)
            {
                int oldIdx = y * oldWidth + x;
                int newIdx = y * width + x;

                if (oldIdx < oldKinds.Length)
                    cellKinds[newIdx] = oldKinds[oldIdx];

                if (oldIdx < oldRequired.Length)
                    requiredClearCounts[newIdx] = oldRequired[oldIdx];
            }
        }
    }

    public HexCellKind GetKind(int x, int y)
    {
        if (!InBounds(x, y))
            return HexCellKind.Empty;

        EnsureCellsSize();
        return cellKinds[y * width + x];
    }

    public int GetRequiredClearCount(int x, int y)
    {
        if (!InBounds(x, y))
            return 0;

        EnsureCellsSize();
        return Mathf.Max(0, requiredClearCounts[y * width + x]);
    }

    public void SetKind(int x, int y, HexCellKind value)
    {
        if (!InBounds(x, y))
            return;

        EnsureCellsSize();
        cellKinds[y * width + x] = value;

       
        if (value != HexCellKind.Locked)
            requiredClearCounts[y * width + x] = 0;
    }

    public void SetRequiredClearCount(int x, int y, int value)
    {
        if (!InBounds(x, y))
            return;

        EnsureCellsSize();
        requiredClearCounts[y * width + x] = Mathf.Max(0, value);
    }

    public void ClearAll()
    {
        EnsureCellsSize();

        for (int i = 0; i < cellKinds.Length; i++)
        {
            cellKinds[i] = HexCellKind.Empty;
            requiredClearCounts[i] = 0;
        }
    }

    public void FillAll()
    {
        EnsureCellsSize();

        for (int i = 0; i < cellKinds.Length; i++)
        {
            cellKinds[i] = HexCellKind.Normal;
            requiredClearCounts[i] = 0;
        }
    }

    public IEnumerable<HexLayoutCellInfo> EnumerateCells()
    {
        EnsureCellsSize();

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var kind = GetKind(x, y);
                if (kind == HexCellKind.Empty)
                    continue;

                yield return new HexLayoutCellInfo
                {
                    coord = OffsetToAxial(x, y),
                    kind = kind,
                    requiredClearCount = GetRequiredClearCount(x, y)
                };
            }
        }
    }

  
    // i used this: https://www.redblobgames.com/grids/hexagons/#conversions-offset
    public Hex OffsetToAxial(int x, int y)
    {
        int ox = centerOnZero ? x - (width / 2) + centerOffsetX : x + centerOffsetX;
        int oy = centerOnZero ? y - (height / 2) + centerOffsetY : y + centerOffsetY;

        int col;
        if (offsetMode == HexGridOffsetMode.OddR)
            col = ox - (oy - (oy & 1)) / 2;
        else
            col = ox - (oy + (oy & 1)) / 2;

        return new Hex(col, oy);
    }

    private bool InBounds(int x, int y)
    {
        return x >= 0 && y >= 0 && x < width && y < height;
    }
}