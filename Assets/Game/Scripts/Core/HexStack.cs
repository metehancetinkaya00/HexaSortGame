using System.Collections.Generic;

public class HexStack
{
    public HexCell cell;

    private readonly List<TileColor> tiles;

    public HexStack(HexCell cell)
    {
        this.cell = cell;
        tiles = new List<TileColor>();
    }

    public int Count => tiles.Count;
    public bool IsEmpty => tiles.Count == 0;

    public TileColor? TopColor
    {
        get
        {
            if (tiles.Count == 0) return null;
            return tiles[tiles.Count - 1];
        }
    }

  
    public IReadOnlyList<TileColor> Snapshot()
    {
        return tiles.AsReadOnly();
    }

    public void SetTiles(IEnumerable<TileColor> newTiles)
    {
        tiles.Clear();
        if (newTiles == null) return;
        tiles.AddRange(newTiles);
    }

    public void PushMany(IEnumerable<TileColor> colors)
    {
        if (colors == null) return;
        tiles.AddRange(colors);
    }

   
    public int TopRunCount()
    {
        if (tiles.Count == 0) return 0;

        TileColor top = tiles[tiles.Count - 1];
        int run = 0;

        for (int i = tiles.Count - 1; i >= 0; i--)
        {
            if (tiles[i] != top) break;
            run++;
        }

        return run;
    }

 
    public List<TileColor> PopTopRun()
    {
        int run = TopRunCount();
        if (run <= 0) return new List<TileColor>();

        int startIndex = tiles.Count - run;
        List<TileColor> removed = tiles.GetRange(startIndex, run);
        tiles.RemoveRange(startIndex, run);

        return removed;
    }

    public bool TryClearTop(int clearCount)
    {
        if (clearCount <= 0) return false;

        int run = TopRunCount();
        if (run < clearCount) return false;

        tiles.RemoveRange(tiles.Count - clearCount, clearCount);
        return true;
    }
}