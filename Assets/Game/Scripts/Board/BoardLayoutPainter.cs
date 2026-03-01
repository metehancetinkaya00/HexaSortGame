using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class BoardLayoutPainter : MonoBehaviour
{
    public BoardLayoutSO layoutAsset;

    public HexCell hexCellPrefab;
    public Transform cellsParent;

    public float yLevel = 0f;
    public bool snapToHexGrid = true;

    private Dictionary<Hex, HexCell> spawnedCells = new Dictionary<Hex, HexCell>();

    void Reset()
    {
        cellsParent = transform;
    }

#if UNITY_EDITOR
    void OnEnable()
    {
        if (cellsParent == null)
            cellsParent = transform;

        SceneView.duringSceneGui += OnSceneGUI;
    }

    void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    void OnSceneGUI(SceneView sceneView)
    {
        if (!enabled) return;
        if (hexCellPrefab == null) return;

        Event currentEvent = Event.current;
        if (currentEvent == null) return;

        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

        if (!currentEvent.shift) return;

        Ray ray = HandleUtility.GUIPointToWorldRay(currentEvent.mousePosition);
        Plane groundPlane = new Plane(Vector3.up, Vector3.up * yLevel);

        float hitDistance;
        if (!groundPlane.Raycast(ray, out hitDistance)) return;

        Vector3 hitPoint = ray.GetPoint(hitDistance);

        Hex hexCoord = snapToHexGrid ? hitPoint.ToHex() : hitPoint.ToHex();

        Handles.Label(hexCoord.ToWorld(yLevel) + Vector3.up * 0.1f, hexCoord.ToString());

        if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0)
        {
            AddCell(hexCoord);
            currentEvent.Use();
        }
        else if (currentEvent.type == EventType.MouseDown && currentEvent.button == 1)
        {
            RemoveCell(hexCoord);
            currentEvent.Use();
        }
    }

    public void AddCell(Hex hexCoord)
    {
        if (cellsParent == null)
            cellsParent = transform;

        if (spawnedCells.ContainsKey(hexCoord))
            return;

        HexCell newCell = (HexCell)PrefabUtility.InstantiatePrefab(hexCellPrefab, cellsParent);
        newCell.Init(hexCoord);
        newCell.Stack.SetTiles(System.Array.Empty<TileColor>());
        newCell.transform.position = hexCoord.ToWorld(yLevel);

        spawnedCells.Add(hexCoord, newCell);
        EditorUtility.SetDirty(gameObject);
    }

    public void RemoveCell(Hex hexCoord)
    {
        if (cellsParent == null)
            cellsParent = transform;

        HexCell existingCell;
        if (spawnedCells.TryGetValue(hexCoord, out existingCell))
        {
            spawnedCells.Remove(hexCoord);
            if (existingCell != null)
                DestroyImmediate(existingCell.gameObject);
        }
        else
        {
            HexCell[] sceneCells = cellsParent.GetComponentsInChildren<HexCell>(true);
            for (int i = 0; i < sceneCells.Length; i++)
            {
                if (sceneCells[i] != null && sceneCells[i].coord.Equals(hexCoord))
                {
                    DestroyImmediate(sceneCells[i].gameObject);
                    break;
                }
            }
        }

        EditorUtility.SetDirty(gameObject);
    }

    public void ClearSceneCells()
    {
        if (cellsParent == null)
            cellsParent = transform;

        // ✅ Clear should NOT rely on spawnedCells. Delete actual scene objects.
        HexCell[] sceneCells = cellsParent.GetComponentsInChildren<HexCell>(true);
        for (int i = 0; i < sceneCells.Length; i++)
        {
            if (sceneCells[i] != null)
                DestroyImmediate(sceneCells[i].gameObject);
        }

        spawnedCells.Clear();
        EditorUtility.SetDirty(gameObject);
    }

    public void LoadFromLayout()
    {
        if (layoutAsset == null) return;

        ClearSceneCells();

        foreach (Hex hexCoord in layoutAsset.EnumerateHexes())
        {
            AddCell(hexCoord);
        }

        RebuildDictionaryFromScene();
        RefreshHexBoardIfExists();
    }

    public void SaveToLayout()
    {
        if (layoutAsset == null) return;

        if (cellsParent == null)
            cellsParent = transform;

        // ✅ Save should read scene objects
        HexCell[] sceneCells = cellsParent.GetComponentsInChildren<HexCell>(true);

        layoutAsset.cells = new List<HexCoordList>();

        HexCoordList oneList = new HexCoordList();
        oneList.items = new List<HexCoord>();

        for (int i = 0; i < sceneCells.Length; i++)
        {
            HexCell cell = sceneCells[i];
            if (cell == null) continue;

            oneList.items.Add(new HexCoord(cell.coord.col, cell.coord.row));
        }

        layoutAsset.cells.Add(oneList);

        EditorUtility.SetDirty(layoutAsset);
        AssetDatabase.SaveAssets();
    }

    void RebuildDictionaryFromScene()
    {
        if (cellsParent == null)
            cellsParent = transform;

        spawnedCells.Clear();

        HexCell[] sceneCells = cellsParent.GetComponentsInChildren<HexCell>(true);
        for (int i = 0; i < sceneCells.Length; i++)
        {
            HexCell cell = sceneCells[i];
            if (cell == null) continue;

            // if someone dragged it manually
            if (cell.Stack == null)
            {
                cell.Init(cell.coord);
                cell.Stack.SetTiles(System.Array.Empty<TileColor>());
            }

            spawnedCells[cell.coord] = cell;
        }
    }

    void RefreshHexBoardIfExists()
    {
        HexBoard board = GameObject.FindObjectOfType<HexBoard>(true);
        if (board != null)
        {
            // Only if you implemented this method on HexBoard:
            // board.RefreshBoardFromScene();
            // If not implemented, it does nothing.
            var method = board.GetType().GetMethod("RefreshBoardFromScene");
            if (method != null)
                method.Invoke(board, null);
        }
    }
#endif
}