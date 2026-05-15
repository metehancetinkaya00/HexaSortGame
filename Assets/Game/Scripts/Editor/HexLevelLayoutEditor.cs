#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(HexLevelLayout))]
public class HexLevelLayoutEditor : Editor
{
    private bool isPainting;
    private int paintMouseButton;

    private float hexRadius = 12f;
    private float hexPadding = 2f;

    private HexCellKind selectedPaintKind = HexCellKind.Normal;
    private int selectedRequiredClearCount = 1;

    public override void OnInspectorGUI()
    {
        var layout = (HexLevelLayout)target;

        EditorGUI.BeginChangeCheck();

        int newWidth = EditorGUILayout.IntField("Width", layout.width);
        int newHeight = EditorGUILayout.IntField("Height", layout.height);

        layout.offsetMode = (HexGridOffsetMode)EditorGUILayout.EnumPopup("Offset Mode", layout.offsetMode);
        layout.centerOnZero = EditorGUILayout.Toggle("Center On Zero", layout.centerOnZero);
        layout.centerOffsetX = EditorGUILayout.IntField("Center Offset X", layout.centerOffsetX);
        layout.centerOffsetY = EditorGUILayout.IntField("Center Offset Y", layout.centerOffsetY);

        GUILayout.Space(8f);

        selectedPaintKind = (HexCellKind)EditorGUILayout.EnumPopup("Paint Kind", selectedPaintKind);

        if (selectedPaintKind == HexCellKind.Locked)
        {
            selectedRequiredClearCount = EditorGUILayout.IntField("Locked Clear Count", selectedRequiredClearCount);
            selectedRequiredClearCount = Mathf.Max(1, selectedRequiredClearCount);
        }

        GUILayout.Space(8f);

        hexRadius = EditorGUILayout.Slider("Hex Radius", hexRadius, 6f, 24f);
        hexPadding = EditorGUILayout.Slider("Hex Padding", hexPadding, 0f, 8f);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(layout, "Change Hex Level Layout");
            layout.Resize(newWidth, newHeight);
            EditorUtility.SetDirty(layout);
        }

        GUILayout.Space(8f);

        GUILayout.BeginHorizontal();

        if (GUILayout.Button("Clear"))
        {
            Undo.RecordObject(layout, "Clear Hex Level Layout");
            layout.ClearAll();
            EditorUtility.SetDirty(layout);
        }

        if (GUILayout.Button("Fill"))
        {
            Undo.RecordObject(layout, "Fill Hex Level Layout");
            layout.FillAll();
            EditorUtility.SetDirty(layout);
        }

        GUILayout.EndHorizontal();

        GUILayout.Space(10f);

        layout.EnsureCellsSize();
        DrawHexGrid(layout);

        if (GUI.changed)
            EditorUtility.SetDirty(layout);
    }

    private void DrawHexGrid(HexLevelLayout layout)
    {
        var e = Event.current;

        if (e.type == EventType.MouseUp)
            isPainting = false;

        float hexW = hexRadius * 2f;
        float hexH = Mathf.Sqrt(3f) * hexRadius;
        float stepY = hexH + hexPadding;
        float stepX = hexW * 0.75f + hexPadding;

        float neededW = layout.width * stepX + hexW;
        float neededH = layout.height * stepY + hexH;

        Rect fullRect = GUILayoutUtility.GetRect(neededW, neededH, GUILayout.ExpandWidth(true));

        float startX = fullRect.xMin + hexRadius + 5f;
        float startY = fullRect.yMin + hexRadius + 5f;

        Handles.BeginGUI();

        for (int y = 0; y < layout.height; y++)
        {
            for (int x = 0; x < layout.width; x++)
            {
              
                bool isOffsetRow = layout.offsetMode == HexGridOffsetMode.OddR
                    ? (y % 2) == 1
                    : (y % 2) == 0;

                float offsetX = isOffsetRow ? stepX * 0.5f : 0f;

                float cx = startX + x * stepX + offsetX;
                float cy = startY + y * stepY;

                var center = new Vector2(cx, cy);
                var polygon = BuildHexPolygon(center, hexRadius);

                var kind = layout.GetKind(x, y);
                int clearCount = layout.GetRequiredClearCount(x, y);

            
                Handles.color = GetFillColor(kind);
                Handles.DrawAAConvexPolygon(polygon);

                // kenar çizgisi
                Handles.color = new Color(0f, 0f, 0f, 0.35f);
                Handles.DrawAAPolyLine(2f,
                    polygon[0], polygon[1], polygon[2],
                    polygon[3], polygon[4], polygon[5], polygon[0]);

                if (kind == HexCellKind.Locked)
                {
                    var style = new GUIStyle(EditorStyles.boldLabel)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        normal = { textColor = Color.white }
                    };
                    GUI.Label(new Rect(cx - 18f, cy - 10f, 36f, 20f), clearCount.ToString(), style);
                }

                bool hovered = IsPointInsidePolygon(e.mousePosition, polygon);

                if (hovered && e.type == EventType.MouseDown)
                {
                    isPainting = true;
                    paintMouseButton = e.button;
                    PaintCell(layout, x, y, paintMouseButton);
                    e.Use();
                }

                if (hovered && isPainting && e.type == EventType.MouseDrag)
                {
                    PaintCell(layout, x, y, paintMouseButton);
                    e.Use();
                }
            }
        }

        Handles.EndGUI();
    }

    private Vector3[] BuildHexPolygon(Vector2 center, float radius)
    {
        var points = new Vector3[6];

        for (int i = 0; i < 6; i++)
        {
            // pointy-top hexagon, -30 derece rotasyon
            float angle = Mathf.Deg2Rad * (60f * i - 30f);
            points[i] = new Vector3(
                center.x + radius * Mathf.Cos(angle),
                center.y + radius * Mathf.Sin(angle),
                0f
            );
        }

        return points;
    }

 
    private bool IsPointInsidePolygon(Vector2 point, Vector3[] polygon)
    {
        bool inside = false;
        int j = polygon.Length - 1;

        for (int i = 0; i < polygon.Length; i++)
        {
            var pi = new Vector2(polygon[i].x, polygon[i].y);
            var pj = new Vector2(polygon[j].x, polygon[j].y);

            bool intersects = ((pi.y > point.y) != (pj.y > point.y)) &&
                              (point.x < (pj.x - pi.x) * (point.y - pi.y) / (pj.y - pi.y + 0.000001f) + pi.x);

            if (intersects)
                inside = !inside;

            j = i;
        }

        return inside;
    }

    private void PaintCell(HexLevelLayout layout, int x, int y, int button)
    {
        Undo.RecordObject(layout, "Paint Hex Cell");


        if (button == 1)
        {
            layout.SetKind(x, y, HexCellKind.Empty);
            layout.SetRequiredClearCount(x, y, 0);
            return;
        }

        layout.SetKind(x, y, selectedPaintKind);

        if (selectedPaintKind == HexCellKind.Locked)
            layout.SetRequiredClearCount(x, y, selectedRequiredClearCount);
        else
            layout.SetRequiredClearCount(x, y, 0);
    }

    private Color GetFillColor(HexCellKind kind)
    {
        return kind switch
        {
            HexCellKind.Normal => new Color(0.25f, 0.75f, 1f, 1f),
            HexCellKind.Locked => new Color(0.75f, 0.45f, 0.15f, 1f),
            _ => new Color(0f, 0f, 0f, 0.10f)  // empty
        };
    }
}
#endif