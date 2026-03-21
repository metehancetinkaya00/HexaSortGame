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
        if (width < 1)
        {
            width = 1;
        }

        if (height < 1)
        {
            height = 1;
        }

        int targetSize = width * height;

        if (cellKinds == null)
        {
            cellKinds = new HexCellKind[targetSize];
        }

        if (requiredClearCounts == null)
        {
            requiredClearCounts = new int[targetSize];
        }

        if (cellKinds.Length != targetSize || requiredClearCounts.Length != targetSize)
        {
            Resize(width, height);
        }
    }

    public void Resize(int newWidth, int newHeight)
    {
        if (newWidth < 1)
        {
            newWidth = 1;
        }

        if (newHeight < 1)
        {
            newHeight = 1;
        }

        int oldWidth = width;
        int oldHeight = height;

        HexCellKind[] oldKinds = cellKinds;
        int[] oldRequired = requiredClearCounts;

        width = newWidth;
        height = newHeight;

        int targetSize = width * height;
        cellKinds = new HexCellKind[targetSize];
        requiredClearCounts = new int[targetSize];

        if (oldKinds == null || oldRequired == null)
        {
            return;
        }

        int copyWidth = Mathf.Min(oldWidth, width);
        int copyHeight = Mathf.Min(oldHeight, height);

        for (int y = 0; y < copyHeight; y++)
        {
            for (int x = 0; x < copyWidth; x++)
            {
                int oldIndex = y * oldWidth + x;
                int newIndex = y * width + x;

                if (oldIndex >= 0 && oldIndex < oldKinds.Length)
                {
                    cellKinds[newIndex] = oldKinds[oldIndex];
                }

                if (oldIndex >= 0 && oldIndex < oldRequired.Length)
                {
                    requiredClearCounts[newIndex] = oldRequired[oldIndex];
                }
            }
        }
    }

    public HexCellKind GetKind(int x, int y)
    {
        if (x < 0 || y < 0 || x >= width || y >= height)
        {
            return HexCellKind.Empty;
        }

        EnsureCellsSize();

        int index = y * width + x;
        return cellKinds[index];
    }

    public int GetRequiredClearCount(int x, int y)
    {
        if (x < 0 || y < 0 || x >= width || y >= height)
        {
            return 0;
        }

        EnsureCellsSize();

        int index = y * width + x;
        return Mathf.Max(0, requiredClearCounts[index]);
    }

    public void SetKind(int x, int y, HexCellKind value)
    {
        if (x < 0 || y < 0 || x >= width || y >= height)
        {
            return;
        }

        EnsureCellsSize();

        int index = y * width + x;
        cellKinds[index] = value;

        if (value != HexCellKind.Locked)
        {
            requiredClearCounts[index] = 0;
        }
    }

    public void SetRequiredClearCount(int x, int y, int value)
    {
        if (x < 0 || y < 0 || x >= width || y >= height)
        {
            return;
        }

        EnsureCellsSize();

        int index = y * width + x;
        requiredClearCounts[index] = Mathf.Max(0, value);
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
                HexCellKind kind = GetKind(x, y);
                if (kind == HexCellKind.Empty)
                {
                    continue;
                }

                HexLayoutCellInfo info = new HexLayoutCellInfo();
                info.coord = OffsetToAxial(x, y);
                info.kind = kind;
                info.requiredClearCount = GetRequiredClearCount(x, y);

                yield return info;
            }
        }
    }

    public Hex OffsetToAxial(int x, int y)
    {
        int offsetX = x;
        int offsetY = y;

        if (centerOnZero)
        {
            offsetX = x - (width / 2) + centerOffsetX;
            offsetY = y - (height / 2) + centerOffsetY;
        }
        else
        {
            offsetX = x + centerOffsetX;
            offsetY = y + centerOffsetY;
        }

        int row = offsetY;
        int col = 0;

        if (offsetMode == HexGridOffsetMode.OddR)
        {
            int shift = (row - (row & 1)) / 2;
            col = offsetX - shift;
        }
        else
        {
            int shift = (row + (row & 1)) / 2;
            col = offsetX - shift;
        }

        return new Hex(col, row);
    }
}