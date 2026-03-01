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
        if (cellsParent == null) cellsParent = transform;
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

        // IMPORTANT: stop Unity selection from eating clicks
        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

        // Use SHIFT (more reliable than CTRL)
        if (!currentEvent.shift) return;

        Ray ray = HandleUtility.GUIPointToWorldRay(currentEvent.mousePosition);
        Plane groundPlane = new Plane(Vector3.up, Vector3.up * yLevel);

        float hitDistance;
        if (!groundPlane.Raycast(ray, out hitDistance)) return;

        Vector3 hitPoint = ray.GetPoint(hitDistance);

        Hex hexCoord;
        if (snapToHexGrid) hexCoord = hitPoint.ToHex();
        else hexCoord = hitPoint.ToHex();

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
        if (spawnedCells.ContainsKey(hexCoord)) return;

        if (cellsParent == null) cellsParent = transform;

        HexCell newCell = (HexCell)PrefabUtility.InstantiatePrefab(hexCellPrefab, cellsParent);
        newCell.Init(hexCoord);
        newCell.transform.position = hexCoord.ToWorld(yLevel);

        spawnedCells.Add(hexCoord, newCell);
        EditorUtility.SetDirty(gameObject);
    }

    public void RemoveCell(Hex hexCoord)
    {
        HexCell existingCell;
        if (!spawnedCells.TryGetValue(hexCoord, out existingCell)) return;

        spawnedCells.Remove(hexCoord);

        if (existingCell != null)
            DestroyImmediate(existingCell.gameObject);

        EditorUtility.SetDirty(gameObject);
    }

    public void ClearSceneCells()
    {
        List<HexCell> allCells = new List<HexCell>();

        foreach (KeyValuePair<Hex, HexCell> pair in spawnedCells)
        {
            if (pair.Value != null) allCells.Add(pair.Value);
        }

        spawnedCells.Clear();

        for (int i = 0; i < allCells.Count; i++)
            DestroyImmediate(allCells[i].gameObject);

        EditorUtility.SetDirty(gameObject);
    }

    public void LoadFromLayout()
    {
        if (layoutAsset == null) return;

        ClearSceneCells();

        foreach (Hex hexCoord in layoutAsset.EnumerateHexes())
            AddCell(hexCoord);
    }

    public void SaveToLayout()
    {
        if (layoutAsset == null) return;

        layoutAsset.cells = new List<HexCoordList>();

        HexCoordList oneList = new HexCoordList();
        oneList.items = new List<HexCoord>();

        foreach (KeyValuePair<Hex, HexCell> pair in spawnedCells)
        {
            Hex hexCoord = pair.Key;
            oneList.items.Add(new HexCoord(hexCoord.col, hexCoord.row));
        }

        layoutAsset.cells.Add(oneList);

        EditorUtility.SetDirty(layoutAsset);
        AssetDatabase.SaveAssets();
    }
#endif
}