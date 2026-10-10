using UnityEditor;
using UnityEngine;

/// <summary>
/// Dedicated editor window for the Onepen terrain tool: one panel for generating,
/// sculpting, painting, baking textures and exporting, so you don't have to keep the
/// TerrainTool selected in the inspector while level-editing. It operates on a chosen
/// <see cref="TerrainTool"/> (assign one, use the selected object, or it auto-picks the
/// selection). All controls are shared with the inspector via
/// <see cref="TerrainToolActions.DrawControlPanel"/>, and the brush settings are shared
/// with the scene-view tool/overlay, so everything stays in sync.
/// </summary>
public class TerrainToolWindow : EditorWindow
{
    [SerializeField] TerrainTool _target;
    Vector2 _scroll;

    [MenuItem("Tools/Onepen Terrain/Terrain Tool Window")]
    public static void Open() => Open(null);

    /// <summary>Open the window, optionally targeting a specific terrain.</summary>
    public static void Open(TerrainTool tool)
    {
        var window = GetWindow<TerrainToolWindow>("Terrain Tool");
        window.minSize = new Vector2(300, 400);
        if (tool != null)
            window._target = tool;
        window.Show();
    }

    void OnEnable() => Selection.selectionChanged += Repaint;
    void OnDisable() => Selection.selectionChanged -= Repaint;

    void OnGUI()
    {
        // Fall back to the selected object's terrain when nothing is pinned.
        if (_target == null && Selection.activeGameObject != null)
            Selection.activeGameObject.TryGetComponent(out _target);

        using (new EditorGUILayout.HorizontalScope())
        {
            _target = (TerrainTool)EditorGUILayout.ObjectField("Terrain", _target, typeof(TerrainTool), allowSceneObjects: true);
            using (new EditorGUI.DisabledScope(!(Selection.activeGameObject != null &&
                                                 Selection.activeGameObject.GetComponent<TerrainTool>() != null)))
            {
                if (GUILayout.Button("Use Selected", GUILayout.Width(90)))
                    _target = Selection.activeGameObject.GetComponent<TerrainTool>();
            }
        }

        EditorGUILayout.Space(8);

        if (_target == null)
        {
            EditorGUILayout.HelpBox("Select a GameObject with a TerrainTool, or assign one above.", MessageType.Info);
            return;
        }

        if (Application.isPlaying)
            EditorGUILayout.HelpBox("In Play mode the runtime brush drives the terrain; edit-mode controls are disabled.", MessageType.Warning);

        using (var scroll = new EditorGUILayout.ScrollViewScope(_scroll))
        {
            _scroll = scroll.scrollPosition;
            TerrainToolActions.DrawControlPanel(_target);
        }
    }
}
