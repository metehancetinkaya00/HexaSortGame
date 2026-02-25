using System.Collections.Generic;
using UnityEngine;

public class HandPiece : MonoBehaviour
{
    public List<TileColor> tiles = new();

    public void SetTiles(List<TileColor> t)
    {
        tiles = t;
        name = $"HandPiece ({tiles.Count})";
    }
}
