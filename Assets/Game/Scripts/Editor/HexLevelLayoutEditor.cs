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

    public override void OnInspectorGUI()
    {
        HexLevelLayout layout = (HexLevelLayout)target;

        EditorGUI.BeginChangeCheck();

        int newWidth = EditorGUILayout.IntField("Width", layout.width);
        int newHeight = EditorGUILayout.IntField("Height", layout.height);

        layout.offsetMode = (HexGridOffsetMode)EditorGUILayout.EnumPopup("Offset Mode", layout.offsetMode);

        layout.centerOnZero = EditorGUILayout.Toggle("Center On Zero", layout.centerOnZero);
        layout.centerOffsetX = EditorGUILayout.IntField("Center Offset X", layout.centerOffsetX);
        layout.centerOffsetY = EditorGUILayout.IntField("Center Offset Y", layout.centerOffsetY);

        hexRadius = EditorGUILayout.Slider("Hex Radius", hexRadius, 6f, 24f);
        hexPadding = EditorGUILayout.Slider("Hex Padding", hexPadding, 0f, 8f);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(layout, "Change Hex Level Layout");
            layout.Resize(newWidth, newHeight);
            EditorUtility.SetDirty(layout);
        }

        GUILayout.Space(8);

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

        GUILayout.Space(10);

        layout.EnsureCellsSize();

        DrawHexGrid(layout);

        if (GUI.changed)
        {
            EditorUtility.SetDirty(layout);
        }
    }

    private void DrawHexGrid(HexLevelLayout layout)
    {
        Event currentEvent = Event.current;

        if (currentEvent.type == EventType.MouseUp)
        {
            isPainting = false;
        }

        float hexWidth = hexRadius * 2f;
        float hexHeight = Mathf.Sqrt(3f) * hexRadius;

        float rowStepY = hexHeight + hexPadding;
        float colStepX = (hexWidth * 0.75f) + hexPadding;

        Rect drawRect = GUILayoutUtility.GetRect(10f, 10f);
        float startX = drawRect.xMin + 10f;
        float startY = drawRect.yMin + 10f;

        float neededWidth = (layout.width * colStepX) + hexWidth;
        float neededHeight = (layout.height * rowStepY) + hexHeight;

        Rect fullRect = GUILayoutUtility.GetRect(neededWidth, neededHeight, GUILayout.ExpandWidth(true));

        startX = fullRect.xMin + hexRadius + 5f;
        startY = fullRect.yMin + hexRadius + 5f;

        Handles.BeginGUI();

        for (int y = 0; y < layout.height; y++)
        {
            for (int x = 0; x < layout.width; x++)
            {
                float offsetX = 0f;

                bool isOffsetRow;
                if (layout.offsetMode == HexGridOffsetMode.OddR)
                {
                    isOffsetRow = (y % 2) == 1;
                }
                else
                {
                    isOffsetRow = (y % 2) == 0;
                }

                if (isOffsetRow)
                {
                    offsetX = colStepX * 0.5f;
                }

                float centerX = startX + (x * colStepX) + offsetX;
                float centerY = startY + (y * rowStepY);

                Vector2 center = new Vector2(centerX, centerY);

                Vector3[] polygon = BuildHexPolygon(center, hexRadius);

                HexCellState state = layout.Get(x, y);
                Color fillColor = GetFillColor(state);

                Handles.color = fillColor;
                Handles.DrawAAConvexPolygon(polygon);

                Handles.color = new Color(0f, 0f, 0f, 0.35f);
                Handles.DrawAAPolyLine(2f, polygon[0], polygon[1], polygon[2], polygon[3], polygon[4], polygon[5], polygon[0]);

                bool hovered = IsPointInsidePolygon(currentEvent.mousePosition, polygon);

                if (hovered && currentEvent.type == EventType.MouseDown)
                {
                    isPainting = true;
                    paintMouseButton = currentEvent.button;
                    PaintCell(layout, x, y, paintMouseButton);
                    currentEvent.Use();
                }

                if (hovered && isPainting && currentEvent.type == EventType.MouseDrag)
                {
                    PaintCell(layout, x, y, paintMouseButton);
                    currentEvent.Use();
                }
            }
        }

        Handles.EndGUI();
    }

    private Vector3[] BuildHexPolygon(Vector2 center, float radius)
    {
        Vector3[] points = new Vector3[6];

        for (int i = 0; i < 6; i++)
        {
            float angleDeg = 60f * i - 30f;
            float angleRad = angleDeg * Mathf.Deg2Rad;

            float px = center.x + radius * Mathf.Cos(angleRad);
            float py = center.y + radius * Mathf.Sin(angleRad);

            points[i] = new Vector3(px, py, 0f);
        }

        return points;
    }

    private bool IsPointInsidePolygon(Vector2 point, Vector3[] polygon)
    {
        bool inside = false;

        int j = polygon.Length - 1;
        for (int i = 0; i < polygon.Length; i++)
        {
            Vector2 pi = new Vector2(polygon[i].x, polygon[i].y);
            Vector2 pj = new Vector2(polygon[j].x, polygon[j].y);

            bool intersect = ((pi.y > point.y) != (pj.y > point.y)) &&
                             (point.x < (pj.x - pi.x) * (point.y - pi.y) / (pj.y - pi.y + 0.000001f) + pi.x);

            if (intersect)
            {
                inside = !inside;
            }

            j = i;
        }

        return inside;
    }

    private void PaintCell(HexLevelLayout layout, int x, int y, int mouseButton)
    {
        Undo.RecordObject(layout, "Paint Hex Cell");

        if (mouseButton == 1)
        {
            layout.Set(x, y, HexCellState.Empty);
        }
        else
        {
            layout.Set(x, y, HexCellState.Filled);
        }
    }

    private Color GetFillColor(HexCellState state)
    {
        if (state == HexCellState.Filled)
        {
            return new Color(0.25f, 0.75f, 1f, 1f);
        }

        return new Color(0f, 0f, 0f, 0.10f);
    }
}
#endif