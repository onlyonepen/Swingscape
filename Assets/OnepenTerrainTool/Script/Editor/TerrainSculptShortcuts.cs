using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.ShortcutManagement;
using UnityEngine;

/// <summary>
/// Editor keyboard shortcuts for the terrain sculpt tool, registered with Unity's
/// Shortcut Manager so they appear under the "Onepen Terrain" category in
/// <b>Edit ▸ Shortcuts</b> and can be freely rebound there. They drive the shared
/// <see cref="TerrainSculptSettings"/>, so changes show up instantly in the tool
/// window, the scene-view overlay and the brush gizmo.
///
/// Scoping: "Activate Sculpt Tool" is global; the brush shortcuts use the SceneView
/// context, so their keys are only reserved while the Scene view is focused (and are
/// further ignored unless the sculpt tool is the active tool). The defaults below are
/// just starting points — rebind or clear any of them in Edit ▸ Shortcuts.
/// </summary>
static class TerrainSculptShortcuts
{
    const string Prefix = "Onepen Terrain/";

    [Shortcut(Prefix + "Activate Sculpt Tool", KeyCode.B, ShortcutModifiers.Shift)]
    static void ActivateTool() => ToolManager.SetActiveTool<TerrainSculptTool>();

    [Shortcut(Prefix + "Toggle Sculpt or Paint", typeof(SceneView), KeyCode.Tab)]
    static void ToggleMode()
    {
        if (!ToolActive)
            return;
        TerrainSculptSettings s = TerrainSculptSettings.instance;
        s.mode = s.mode == TerrainTool.BrushMode.Sculpt
            ? TerrainTool.BrushMode.Paint
            : TerrainTool.BrushMode.Sculpt;
        Commit(s);
    }

    [Shortcut(Prefix + "Increase Brush Radius", typeof(SceneView), KeyCode.RightBracket)]
    static void IncreaseRadius() => NudgeRadius(+1f);

    [Shortcut(Prefix + "Decrease Brush Radius", typeof(SceneView), KeyCode.LeftBracket)]
    static void DecreaseRadius() => NudgeRadius(-1f);

    // Material slots — unbound by default (assign keys in Edit ▸ Shortcuts if you want them).
    [Shortcut(Prefix + "Paint Slot 1", typeof(SceneView))] static void Slot1() => SetSlot(0);
    [Shortcut(Prefix + "Paint Slot 2", typeof(SceneView))] static void Slot2() => SetSlot(1);
    [Shortcut(Prefix + "Paint Slot 3", typeof(SceneView))] static void Slot3() => SetSlot(2);
    [Shortcut(Prefix + "Paint Slot 4", typeof(SceneView))] static void Slot4() => SetSlot(3);

    static void NudgeRadius(float delta)
    {
        if (!ToolActive)
            return;
        TerrainSculptSettings s = TerrainSculptSettings.instance;
        s.radius = Mathf.Clamp(s.radius + delta, 0.1f, 50f);
        Commit(s);
    }

    static void SetSlot(int slice)
    {
        if (!ToolActive)
            return;
        TerrainSculptSettings s = TerrainSculptSettings.instance;
        s.selectedMaterial = Mathf.Clamp(slice, 0, MatSplat.SlotCount - 1);
        Commit(s);
    }

    static bool ToolActive => ToolManager.activeToolType == typeof(TerrainSculptTool);

    static void Commit(TerrainSculptSettings s)
    {
        s.Persist();
        SceneView.RepaintAll();
    }
}
