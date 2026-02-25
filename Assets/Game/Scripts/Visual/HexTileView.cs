using UnityEngine;

public class HexTileView : MonoBehaviour
{
    public TileColor color;
    public int indexInStack;

    [SerializeField] private Renderer rend;

    public void Init(TileColor c, int index, Material mat)
    {
        color = c;
        indexInStack = index;

        if (!rend) rend = GetComponentInChildren<Renderer>();
        if (rend && mat) rend.sharedMaterial = mat;
    }
}
