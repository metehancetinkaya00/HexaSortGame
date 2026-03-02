using System.Collections.Generic;
using UnityEngine;

public enum HexGridOffsetMode
{
    OddR = 0,
    EvenR = 1
}

public enum HexCellState
{
    Empty = 0,
    Filled = 1
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

    public HexCellState[] cells;

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

        if (cells == null)
        {
            cells = new HexCellState[targetSize];
            return;
        }

        if (cells.Length != targetSize)
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
        HexCellState[] oldCells = cells;

        width = newWidth;
        height = newHeight;

        int targetSize = width * height;
        cells = new HexCellState[targetSize];

        if (oldCells == null)
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

                if (oldIndex >= 0 && oldIndex < oldCells.Length && newIndex >= 0 && newIndex < cells.Length)
                {
                    cells[newIndex] = oldCells[oldIndex];
                }
            }
        }
    }

    public HexCellState Get(int x, int y)
    {
        if (x < 0 || y < 0 || x >= width || y >= height)
        {
            return HexCellState.Empty;
        }

        if (cells == null)
        {
            return HexCellState.Empty;
        }

        int index = y * width + x;

        if (index < 0 || index >= cells.Length)
        {
            return HexCellState.Empty;
        }

        return cells[index];
    }

    public void Set(int x, int y, HexCellState value)
    {
        if (x < 0 || y < 0 || x >= width || y >= height)
        {
            return;
        }

        EnsureCellsSize();

        int index = y * width + x;

        if (index < 0 || index >= cells.Length)
        {
            return;
        }

        cells[index] = value;
    }

    public void ClearAll()
    {
        EnsureCellsSize();

        for (int i = 0; i < cells.Length; i++)
        {
            cells[i] = HexCellState.Empty;
        }
    }

    public void FillAll()
    {
        EnsureCellsSize();

        for (int i = 0; i < cells.Length; i++)
        {
            cells[i] = HexCellState.Filled;
        }
    }

    public IEnumerable<Hex> EnumerateHexes()
    {
        EnsureCellsSize();

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (Get(x, y) != HexCellState.Filled)
                {
                    continue;
                }

                yield return OffsetToAxial(x, y);
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