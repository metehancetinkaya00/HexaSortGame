using System.Collections.Generic;
using UnityEngine;

public class HexCell : MonoBehaviour
{
    public Hex coord;
    public HexStack Stack { get; private set; }

    public List<GameObject> views = new List<GameObject>();

    public void Init(Hex coord)
    {
        this.coord = coord;
        Stack = new HexStack(this);
        name = $"Cell {coord}";
    }
}