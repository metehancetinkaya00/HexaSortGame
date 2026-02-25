using System.Collections.Generic;
using System.Linq;

public class HexStack
{
    public readonly HexCell cell;
    private readonly List<TileColor> tiles = new(); 

    public HexStack(HexCell cell)
    {
        this.cell = cell;
    }

    public int Count => tiles.Count;
    public bool IsEmpty => tiles.Count == 0;

    public TileColor? TopColor => IsEmpty ? null : tiles[^1];

    public IReadOnlyList<TileColor> Snapshot() => tiles;

    public void SetTiles(IEnumerable<TileColor> newTiles)
    {
        tiles.Clear();
        tiles.AddRange(newTiles);
    }

    public void PushMany(IEnumerable<TileColor> colors)
    {
        tiles.AddRange(colors);
    }

    public int TopRunCount()
    {
        if (IsEmpty) return 0;

        TileColor c = tiles[^1];
        int run = 0;
        for (int i = tiles.Count - 1; i >= 0; i--)
        {
            if (tiles[i] != c) break;
            run++;
        }
        return run;
    }

  
    public List<TileColor> PopTopRun()
    {
        int run = TopRunCount();
        if (run <= 0) return new List<TileColor>();

        TileColor c = tiles[^1];
        tiles.RemoveRange(tiles.Count - run, run);
        return Enumerable.Repeat(c, run).ToList();
    }

    public bool TryClearTop(int clearCount)
    {
        if (TopRunCount() < clearCount) return false;
        tiles.RemoveRange(tiles.Count - clearCount, clearCount);
        return true;
    }
}
