using System.Collections.Generic;
using UnityEngine;

public class StackPiece : MonoBehaviour
{
    public List<TileColor> tiles = new List<TileColor>(); 

    public void SetTiles(List<TileColor> t)
    {
        tiles = t;
        name = $"Piece ({tiles.Count})";
    }
}
