#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BoardLayoutPainter))]
public class BoardLayoutPainterEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        BoardLayoutPainter painter = (BoardLayoutPainter)target;

        GUILayout.Space(10);
        GUILayout.Label("Painter Buttons", EditorStyles.boldLabel);

        if (GUILayout.Button("Load From Layout"))
            painter.LoadFromLayout();

        if (GUILayout.Button("Save To Layout"))
            painter.SaveToLayout();

        if (GUILayout.Button("Clear Scene Cells"))
            painter.ClearSceneCells();

        GUILayout.Space(8);
        EditorGUILayout.HelpBox(
            "Scene View Controls:\n" +
            "SHIFT + Left Click  = Add Cell\n" +
            "SHIFT + Right Click = Remove Cell\n",
            MessageType.Info
        );
    }
}
#endif