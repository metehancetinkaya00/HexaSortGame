using System.Collections.Generic;
using UnityEngine;

public class HexCell : MonoBehaviour
{
    [Header("Coord (runtime set)")]
    public Hex coord;

    public HexStack Stack { get; private set; }

    
    public List<GameObject> views = new List<GameObject>();

    public void Init(Hex newCoord)
    {
        coord = newCoord;
        Stack = new HexStack(this);
        name = $"Cell {coord}";
    }
}