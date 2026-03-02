using System.Collections.Generic;
using UnityEngine;

public class HandPiece : MonoBehaviour
{
    public List<TileColor> tiles = new();

    public void SetTiles(List<TileColor> t)
    {
        if (t == null)
        {
            tiles = new List<TileColor>();
        }
        else
        {
            tiles = new List<TileColor>();

            for (int i = 0; i < t.Count; i++)
            {
                tiles.Add(t[i]);
            }
        }

        name = "HandPiece (" + tiles.Count + ")";
    }
}