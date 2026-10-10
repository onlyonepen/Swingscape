using UnityEditor;
using UnityEngine;

/// <summary>
/// TerrainTool inspector. The edit-mode sculpting controls, texture bake and export
/// buttons all live in <see cref="TerrainToolActions.DrawControlPanel"/>, shared with
/// the dedicated <see cref="TerrainToolWindow"/> so both stay identical. Sculpting and
/// export work in edit mode — the terrain no longer has to be generated at runtime first.
/// </summary>
[CustomEditor(typeof(TerrainTool))]
public class TerrainToolEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var tool = (TerrainTool)target;

        EditorGUILayout.Space(8);
        if (GUILayout.Button("Open Terrain Tool Window"))
            TerrainToolWindow.Open(tool);

        EditorGUILayout.Space(8);
        TerrainToolActions.DrawControlPanel(tool);
    }
}
