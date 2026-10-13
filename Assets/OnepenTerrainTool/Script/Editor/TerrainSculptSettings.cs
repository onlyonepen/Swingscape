using UnityEditor;
using UnityEngine;

/// <summary>
/// The single owner of all terrain tool configuration. The runtime <see cref="TerrainTool"/>
/// carries no serialized value config (only asset references); these settings are PUSHED
/// into it via <see cref="PushTo"/> before the editor generates or sculpts. Used by the
/// scene-view <see cref="TerrainSculptTool"/>, its overlay, and the <see cref="TerrainToolWindow"/>.
/// Being a <see cref="ScriptableSingleton{T}"/> it survives domain reloads and (via the
/// FilePath) Unity restarts. Editor-only — it never ships in a build.
/// </summary>
[FilePath("UserSettings/OnepenTerrainSculpt.asset", FilePathAttribute.Location.ProjectFolder)]
public class TerrainSculptSettings : ScriptableSingleton<TerrainSculptSettings>
{
    // ---- Grid / resolution (set before generating; the terrain stores at this one cell size) ----
    public int chunkSize = 8;
    public float cellSize = 0.5f; // determined at generation — the terrain's single stored resolution
    public int maxLod = 3;        // coarsest Lod the chunk size supports; the ceiling for the view-distance LOD
    public float isoLevel = 0f;
    public bool smoothShading = false;

    // ---- Initial terrain (slab) ----
    public Vector3Int initialChunks = new Vector3Int(8, 4, 8);
    public float slabThickness = 8f;
    public float slabMargin = 2f;

    // ---- Brush ----
    /// <summary>When a stroke's edits are applied.</summary>
    public enum ApplyMode
    {
        OnRelease, // cache the drawn path (ghost preview) and apply it all at once on mouse-up
        OnMove,    // apply continuously as the cursor moves while held
        WhileHeld, // apply continuously while the button is down, even when stationary
    }

    public ApplyMode applyMode = ApplyMode.OnRelease;
    public TerrainTool.BrushMode mode = TerrainTool.BrushMode.Build;
    public float radius = 2f;
    public float strength = 8f;
    public float smoothStrength = 3f; // per-dab blur fraction for the Smooth brush
    public int selectedMaterial = 0;
    public float paintStrength = 4f;
    public float paintEdgeBlend = 0.3f;

    /// <summary>Show the in-scene brush settings box while the sculpt tool is active.</summary>
    public bool showSceneOverlay = true;

    // ---- Cleanup ----
    // One-click removal of sculpting junk: small disconnected solid blobs (debris) and
    // small fully-enclosed air pockets (holes). Clusters up to this many voxels are affected.
    public int cleanupMaxVoxels = 64;
    public bool cleanupRemoveDebris = true;
    public bool cleanupFillHoles = true;

    // ---- Auto-save ----
    // The edit-mode terrain is a transient session (chunk density isn't serialized in the
    // scene), so without this a recompile / scene reload / entering Play loses your edits.
    // When on, the sculpt tool serializes the terrain to a .bytes asset shortly after you
    // stop editing (and flushes before reloads/Play), keeping it assigned to startupData.
    public bool autoSave = true;
    /// <summary>Seconds of no edits before a pending auto-save is written (debounce).</summary>
    public float autoSaveDelay = 3f;

    // ---- Editor undo bridge ----
    // Bumped once per stroke and recorded with Undo.RegisterCompleteObjectUndo(this),
    // so native Ctrl+Z/Ctrl+Y move it and the sculpt tool can restore the matching snapshot.
    public int editorUndoVersion;

    /// <summary>Persist the current values (call after an edit so they survive a reload).</summary>
    public void Persist() => Save(true);

    /// <summary>
    /// Push the value configuration into a <see cref="TerrainTool"/>'s non-serialized
    /// runtime fields and (re)initialize it. Asset references (material/textures/startup)
    /// stay on the component and are left untouched. Call before generating or sculpting.
    /// </summary>
    public void PushTo(TerrainTool tool)
    {
        if (tool == null)
            return;

        tool.chunkSize = chunkSize;
        tool.cellSize = cellSize;
        tool.maxLod = maxLod;
        tool.defaultLod = 0; // stored at full density (the cell size); the view-distance LOD only changes render resolution
        tool.isoLevel = isoLevel;
        tool.smoothShading = smoothShading;
        tool.initialChunks = initialChunks;
        tool.slabThickness = slabThickness;
        tool.slabMargin = slabMargin;

        tool.EnsureInitialized(); // validates the grid and binds the paint texture array
    }
}
