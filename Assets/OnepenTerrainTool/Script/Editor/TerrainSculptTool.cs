using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

/// <summary>
/// Scene-view tool that lets you sculpt and paint a <see cref="TerrainTool"/>
/// WITHOUT entering Play mode. Activate it from the scene-view tools toolbar while
/// a TerrainTool is selected, then drag over the terrain to edit it.
///
/// The actual editing reuses <see cref="TerrainTool.Sculpt"/>/<see cref="TerrainTool.Paint"/>
/// unchanged — all this tool adds is the editor driving layer that <see cref="TerrainTool.Update"/>
/// provides at runtime: a scene-view raycast, a brush gizmo, and mouse input. Brush
/// settings live in the shared <see cref="TerrainSculptSettings"/>, which can be synced
/// to/from the TerrainTool's own brush fields, so runtime and editor brushes stay consistent.
///
/// Undo: because the voxel arrays aren't serialized, Unity's own RecordObject can't
/// capture them. Instead each stroke snapshots the exact pre-edit state of every
/// chunk it touches (via <see cref="TerrainTool.BeforeChunkEdit"/>, which also
/// catches LOD-balanced neighbours and newly-created chunks) and bumps a serialized
/// version counter registered with <c>Undo.RegisterCompleteObjectUndo</c>. Native
/// Ctrl+Z/Ctrl+Y then move that counter, and <see cref="OnUndoRedo"/> restores the
/// matching snapshot. Session-based: snapshots live in memory, so undo history is
/// lost on script recompile / scene reload (the terrain itself is too — save it to
/// a .bytes asset from the inspector to keep it).
/// </summary>
[EditorTool("Terrain Sculpt", typeof(TerrainTool))]
public class TerrainSculptTool : EditorTool
{
    // How much one mouse-drag "dab" advances toward the full brush effect. Editor
    // input has no Time.deltaTime, so dabs use this fixed per-event step instead.
    const float DabStep = 0.12f;

    // Brush settings live in a shared singleton so the window, overlay and this
    // tool all read/write the same values.
    static TerrainSculptSettings S => TerrainSculptSettings.instance;

    GUIContent _icon;
    Rect _overlayRect;

    // ---- Undo bookkeeping --------------------------------------------------

    class ChunkSnapshot
    {
        public bool existed;
        public float[,,] density;
        public MatSplat[,,] material;
        public int lod, chunkSize;
        public float cellSize;
    }

    class Stroke
    {
        public Dictionary<Vector3Int, ChunkSnapshot> before;
        public Dictionary<Vector3Int, ChunkSnapshot> after;
    }

    readonly Dictionary<int, Stroke> _strokes = new Dictionary<int, Stroke>();
    int _versionCounter;
    int _lastVersion;

    bool _stroking;
    Dictionary<Vector3Int, ChunkSnapshot> _strokeBefore;

    // Deferred "preview then apply on release" stroke: while the mouse is held we only
    // cache the drawn path (and show a ghost preview along it); the actual sculpt/paint
    // is applied all at once on release, as a single undo step.
    bool _previewing;
    bool _strokeRemove; // carve (Shift) vs build, captured when the stroke starts
    readonly List<Vector3> _strokePath = new List<Vector3>();
    readonly List<Vector3> _strokeNormals = new List<Vector3>();

    // Live stroke (OnMove / WhileHeld apply modes): edits are applied immediately
    // during the hold rather than cached. One undo step still spans the whole hold.
    bool _dragging;
    int _strokeControlId;
    Vector3 _lastHoverPoint;
    bool _lastHoverValid;
    double _lastApplyTime;
    const double WhileHeldInterval = 0.03; // seconds between dabs while the button is held stationary

    TerrainTool Tool => target as TerrainTool;

    public override GUIContent toolbarIcon =>
        _icon ??= new GUIContent(EditorGUIUtility.IconContent("TerrainInspector.TerrainToolSculpt").image,
                                 "Terrain Sculpt — drag over the terrain to sculpt/paint in edit mode");

    public override void OnActivated()
    {
        TerrainTool tool = Tool;
        if (tool == null)
            return;

        S.PushTo(tool); // config lives in the settings now — push it in and initialize

        // Recompile / scene reload wipes the transient edit-mode chunks. If auto-save kept a
        // startupData around, reload it so sculpting resumes where it left off.
        if (!HasAnyChunk(tool) && tool.startupData != null && tool.startupData.bytes != null
            && tool.startupData.bytes.Length > 0)
        {
            tool.GenerateOrLoad();
            SceneView.RepaintAll();
        }

        _versionCounter = S.editorUndoVersion;
        _lastVersion = S.editorUndoVersion;
        Undo.undoRedoPerformed += OnUndoRedo;
        EditorApplication.update += AutoSaveTick;
        AssemblyReloadEvents.beforeAssemblyReload += FlushAutoSave;   // survive a recompile
        EditorApplication.playModeStateChanged += OnPlayModeChanged;  // survive entering Play
    }

    public override void OnWillBeDeactivated()
    {
        Undo.undoRedoPerformed -= OnUndoRedo;
        EditorApplication.update -= WhileHeldTick; // safety: never leave the tick subscribed
        EditorApplication.update -= AutoSaveTick;
        AssemblyReloadEvents.beforeAssemblyReload -= FlushAutoSave;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        CancelPreview();  // discard an un-applied preview stroke
        EndLiveStroke();  // commit a live stroke that's in progress
        EndStrokeIfAny(); // and don't leave a half-captured apply behind
        FlushAutoSave();  // persist anything still pending before the tool goes away
    }

    static bool HasAnyChunk(TerrainTool tool)
    {
        foreach (var _ in tool.Chunks)
            return true;
        return false;
    }

    void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.ExitingEditMode)
            FlushAutoSave(); // write pending edits so Play loads the latest
    }

    public override void OnToolGUI(EditorWindow window)
    {
        if (!(window is SceneView) || Tool == null)
            return;

        Event e = Event.current;

        DrawSettingsOverlay();

        // Reserve a passive control so clicks don't fall through to object selection.
        int controlId = GUIUtility.GetControlID(FocusType.Passive);
        if (e.type == EventType.Layout)
            HandleUtility.AddDefaultControl(controlId);

        bool hasHover = TryRaycast(e.mousePosition, out Vector3 point, out Vector3 normal);

        DrawStrokePreview();                 // the cached path (ghost spheres) while held
        if (hasHover)
            DrawBrushGizmo(point, normal);   // the live cursor brush (leading edge of the stroke)

        switch (e.type)
        {
            case EventType.MouseMove:
                window.Repaint(); // keep the gizmo tracking the cursor
                break;

            case EventType.KeyDown when e.keyCode == KeyCode.LeftShift || e.keyCode == KeyCode.RightShift:
            case EventType.KeyUp when e.keyCode == KeyCode.LeftShift || e.keyCode == KeyCode.RightShift:
                window.Repaint(); // recolor the gizmo (add vs carve) as Shift is held/released
                break;

            case EventType.KeyDown when _previewing && e.keyCode == KeyCode.Escape:
                CancelPreview(); // bail out of a stroke without applying it
                e.Use();
                break;

            // Left-click starts a stroke; Shift+Left-click carves. Right mouse is left
            // free for scene-view camera navigation. OnRelease caches the drawn path and
            // commits on release; OnMove / WhileHeld apply live during the hold.
            case EventType.MouseDown when e.button == 0 && hasHover && !e.alt
                                          && !_overlayRect.Contains(e.mousePosition):
                if (S.applyMode == TerrainSculptSettings.ApplyMode.OnRelease)
                    BeginPreview(point, normal, e.shift);
                else
                    BeginLiveStroke(controlId, point, e.shift, hasHover);
                e.Use();
                break;

            case EventType.MouseDrag when _previewing && !e.alt:
                if (hasHover)
                    AddPreviewPoint(point, normal);
                window.Repaint();
                e.Use();
                break;

            case EventType.MouseDrag when _dragging && !e.alt:
                _lastHoverValid = hasHover;
                if (hasHover)
                {
                    _lastHoverPoint = point;
                    if (S.applyMode == TerrainSculptSettings.ApplyMode.OnMove)
                        ApplyDab(point, _strokeRemove); // apply on every move
                }
                window.Repaint();
                e.Use();
                break;

            case EventType.MouseUp when _previewing:
                ExecutePreview(); // apply the whole cached path at once, one undo step
                e.Use();
                break;

            case EventType.MouseUp when _dragging:
                EndLiveStroke();
                e.Use();
                break;
        }
    }

    // ---- Raycast & gizmo ---------------------------------------------------

    bool TryRaycast(Vector2 guiPoint, out Vector3 point, out Vector3 normal)
    {
        point = default;
        normal = Vector3.up;

        Ray ray = HandleUtility.GUIPointToWorldRay(guiPoint);
        if (!Physics.Raycast(ray, out RaycastHit hit, 100000f))
            return false;
        if (!hit.collider.TryGetComponent(out TerrainChunk chunk) || chunk.GetComponentInParent<TerrainTool>() != Tool)
            return false;

        point = hit.point;
        normal = hit.normal;
        return true;
    }

    /// <summary>Brush tint: blue for paint; green = add, red = carve for sculpt.</summary>
    static Color BrushColor(bool remove)
    {
        if (S.mode == TerrainTool.BrushMode.Paint)
            return new Color(0.3f, 0.8f, 1f);
        return remove ? new Color(1f, 0.35f, 0.3f) : new Color(0.3f, 0.9f, 0.4f);
    }

    void DrawBrushGizmo(Vector3 point, Vector3 normal)
    {
        Color c = BrushColor(_previewing || _dragging ? _strokeRemove : Event.current.shift);

        Handles.color = c;
        Handles.DrawWireDisc(point, normal, S.radius);
        if (S.mode == TerrainTool.BrushMode.Paint)
        {
            float inner = S.radius * (1f - Mathf.Clamp01(S.paintEdgeBlend));
            Handles.color = new Color(c.r, c.g, c.b, 0.5f);
            Handles.DrawWireDisc(point, normal, inner);
        }
        Handles.color = new Color(c.r, c.g, c.b, 0.08f);
        Handles.DrawSolidDisc(point, normal, S.radius);
    }

    /// <summary>Draw the cached stroke as a translucent "holographic" path of brush spheres.</summary>
    void DrawStrokePreview()
    {
        if (!_previewing || _strokePath.Count == 0)
            return;

        Color c = BrushColor(_strokeRemove);
        Vector3 toCam = SceneView.currentDrawingSceneView != null
            ? SceneView.currentDrawingSceneView.camera.transform.forward
            : Vector3.forward;

        for (int i = 0; i < _strokePath.Count; i++)
        {
            Vector3 p = _strokePath[i];
            Vector3 n = _strokeNormals[i];

            Handles.color = new Color(c.r, c.g, c.b, 0.07f);
            Handles.DrawSolidDisc(p, n, S.radius);           // soft fill on the surface
            Handles.color = new Color(c.r, c.g, c.b, 0.5f);
            Handles.DrawWireDisc(p, n, S.radius);            // brush outline
            Handles.color = new Color(c.r, c.g, c.b, 0.25f);
            Handles.DrawWireDisc(p, toCam, S.radius);        // camera-facing ring → reads as a sphere
        }

        if (_strokePath.Count >= 2)
        {
            Handles.color = new Color(c.r, c.g, c.b, 0.8f);
            Handles.DrawAAPolyLine(4f, _strokePath.ToArray()); // the drawn path
        }
    }

    // ---- Editing -----------------------------------------------------------

    void ApplyDab(Vector3 point, bool remove)
    {
        TerrainTool tool = Tool;
        if (tool == null)
            return;

        if (S.mode == TerrainTool.BrushMode.Paint)
        {
            float rate = Mathf.Clamp01(S.paintStrength * DabStep);
            tool.Paint(point, S.selectedMaterial, rate, S.radius, S.paintEdgeBlend, S.cellSize);
            return;
        }

        float sign = remove ? 1f : -1f; // left-click builds up (lower density = solid); Shift+left-click carves
        tool.Sculpt(point, sign * S.strength * DabStep, S.radius, S.cellSize);
    }

    // ---- Deferred preview stroke -------------------------------------------

    void BeginPreview(Vector3 point, Vector3 normal, bool remove)
    {
        _previewing = true;
        _strokeRemove = remove;
        _strokePath.Clear();
        _strokeNormals.Clear();
        _strokePath.Add(point);
        _strokeNormals.Add(normal);
    }

    void AddPreviewPoint(Vector3 point, Vector3 normal)
    {
        // Decimate: only record a new sphere once the cursor has moved a fraction of the
        // brush radius, so the path is a clean row of spheres (and the apply stays cheap).
        float minSpacing = Mathf.Max(S.radius * 0.3f, 0.01f);
        if (_strokePath.Count > 0 && Vector3.Distance(_strokePath[_strokePath.Count - 1], point) < minSpacing)
            return;
        _strokePath.Add(point);
        _strokeNormals.Add(normal);
    }

    /// <summary>Apply the whole cached path in one batch, as a single undo step.</summary>
    void ExecutePreview()
    {
        _previewing = false;

        TerrainTool tool = Tool;
        if (tool == null || _strokePath.Count == 0)
        {
            ClearPreview();
            return;
        }

        BeginStroke(); // open the undo-capture window
        foreach (Vector3 p in _strokePath)
            ApplyDab(p, _strokeRemove);
        EndStrokeIfAny(); // commit everything the path touched as one undo step

        ClearPreview();
        SceneView.RepaintAll();
    }

    void CancelPreview()
    {
        if (!_previewing && _strokePath.Count == 0)
            return;
        _previewing = false;
        ClearPreview();
        SceneView.RepaintAll();
    }

    void ClearPreview()
    {
        _strokePath.Clear();
        _strokeNormals.Clear();
    }

    // ---- Live stroke (OnMove / WhileHeld) ----------------------------------

    void BeginLiveStroke(int controlId, Vector3 point, bool remove, bool hasHover)
    {
        _dragging = true;
        _strokeControlId = controlId;
        _strokeRemove = remove;
        _lastHoverPoint = point;
        _lastHoverValid = hasHover;

        GUIUtility.hotControl = controlId; // capture so we reliably get MouseUp, even off-window

        BeginStroke();            // one undo-capture window spans the whole hold
        ApplyDab(point, remove);  // first dab on press

        if (S.applyMode == TerrainSculptSettings.ApplyMode.WhileHeld)
        {
            _lastApplyTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += WhileHeldTick; // keeps applying while stationary
        }
    }

    /// <summary>WhileHeld: apply a dab at the last hover point at a steady rate while the button is down.</summary>
    void WhileHeldTick()
    {
        if (!_dragging)
            return;

        double now = EditorApplication.timeSinceStartup;
        if (now - _lastApplyTime < WhileHeldInterval)
            return;
        _lastApplyTime = now;

        if (_lastHoverValid)
        {
            ApplyDab(_lastHoverPoint, _strokeRemove);
            SceneView.RepaintAll();
        }
    }

    void EndLiveStroke()
    {
        if (!_dragging)
            return;
        _dragging = false;

        EditorApplication.update -= WhileHeldTick;
        if (GUIUtility.hotControl == _strokeControlId)
            GUIUtility.hotControl = 0;

        EndStrokeIfAny(); // commit the whole hold as one undo step
        SceneView.RepaintAll();
    }

    // ---- Stroke / undo -----------------------------------------------------

    void BeginStroke()
    {
        TerrainTool tool = Tool;
        if (tool == null)
            return;

        _stroking = true;
        _strokeBefore = new Dictionary<Vector3Int, ChunkSnapshot>();
        tool.BeforeChunkEdit += OnBeforeChunkEdit; // lazily snapshots each chunk the moment before it first changes
    }

    void OnBeforeChunkEdit(Vector3Int coord)
    {
        if (_strokeBefore.ContainsKey(coord))
            return; // already captured this chunk's pre-stroke state
        _strokeBefore[coord] = Capture(Tool, coord);
    }

    void EndStrokeIfAny()
    {
        if (!_stroking)
            return;

        TerrainTool tool = Tool;
        _stroking = false;
        if (tool != null)
            tool.BeforeChunkEdit -= OnBeforeChunkEdit;

        if (tool == null || _strokeBefore == null || _strokeBefore.Count == 0)
        {
            _strokeBefore = null;
            return;
        }

        var after = new Dictionary<Vector3Int, ChunkSnapshot>(_strokeBefore.Count);
        foreach (Vector3Int coord in _strokeBefore.Keys)
            after[coord] = Capture(tool, coord);

        // Record the pre-stroke counter value on the settings object, then bump it; native
        // Ctrl+Z restores the old value and fires OnUndoRedo, which replays the snapshot.
        TerrainSculptSettings s = S;
        Undo.RegisterCompleteObjectUndo(s, s.mode == TerrainTool.BrushMode.Paint ? "Terrain Paint" : "Terrain Sculpt");
        int version = ++_versionCounter;
        s.editorUndoVersion = version;
        EditorUtility.SetDirty(s);

        _strokes[version] = new Stroke { before = _strokeBefore, after = after };
        _lastVersion = version;
        _strokeBefore = null;

        ScheduleAutoSave(); // a stroke just changed the terrain — queue a debounced save
    }

    void OnUndoRedo()
    {
        TerrainTool tool = Tool;
        if (tool == null)
            return;

        int newVersion = S.editorUndoVersion;
        if (newVersion == _lastVersion)
            return;

        // Replay every stroke crossed (usually just one), oldest-effect-last so
        // chunks shared between strokes settle on the correct final state.
        if (newVersion < _lastVersion)
        {
            for (int v = _lastVersion; v > newVersion; v--) // undo: revert each to its pre-stroke state
                if (_strokes.TryGetValue(v, out Stroke undone))
                    RestoreAll(tool, undone.before);
        }
        else
        {
            for (int v = _lastVersion + 1; v <= newVersion; v++) // redo: re-apply each stroke's result
                if (_strokes.TryGetValue(v, out Stroke redone))
                    RestoreAll(tool, redone.after);
        }

        _lastVersion = newVersion;
        SceneView.RepaintAll();
        ScheduleAutoSave(); // undo/redo changed the terrain too
    }

    void RestoreAll(TerrainTool tool, Dictionary<Vector3Int, ChunkSnapshot> state)
    {
        foreach (KeyValuePair<Vector3Int, ChunkSnapshot> kv in state)
            RestoreChunk(tool, kv.Key, kv.Value);

        // Rebuild the restored chunks and their neighbours so shared seams redraw.
        foreach (Vector3Int coord in state.Keys)
        {
            tool.RebuildChunk(coord);
            foreach (Vector3Int offset in FaceOffsets)
                tool.RebuildChunk(coord + offset);
        }
    }

    static readonly Vector3Int[] FaceOffsets =
    {
        new Vector3Int(-1, 0, 0), new Vector3Int(1, 0, 0),
        new Vector3Int(0, -1, 0), new Vector3Int(0, 1, 0),
        new Vector3Int(0, 0, -1), new Vector3Int(0, 0, 1),
    };

    static void RestoreChunk(TerrainTool tool, Vector3Int coord, ChunkSnapshot snap)
    {
        if (snap == null || !snap.existed)
        {
            tool.DestroyChunk(coord); // the stroke created it — undo removes it
            return;
        }

        TerrainChunk chunk = tool.GetOrCreateChunk(coord, snap.lod);
        chunk.ApplyResampled((float[,,])snap.density.Clone(), (MatSplat[,,])snap.material.Clone(),
                             snap.lod, snap.chunkSize, snap.cellSize);
    }

    static ChunkSnapshot Capture(TerrainTool tool, Vector3Int coord)
    {
        if (tool == null || !tool.TryGetChunk(coord, out TerrainChunk chunk))
            return new ChunkSnapshot { existed = false };

        return new ChunkSnapshot
        {
            existed = true,
            density = (float[,,])chunk.Density.Clone(),   // copy now — the live arrays mutate in place during the stroke
            material = (MatSplat[,,])chunk.Material.Clone(),
            lod = chunk.Lod,
            chunkSize = chunk.ChunkSize,
            cellSize = chunk.CellSize,
        };
    }

    // ---- Auto-save ---------------------------------------------------------

    bool _autoSavePending;
    double _autoSaveDueAt;

    /// <summary>Queue a debounced auto-save; it fires once editing has been idle for autoSaveDelay seconds.</summary>
    void ScheduleAutoSave()
    {
        if (!S.autoSave)
            return;
        _autoSavePending = true;
        _autoSaveDueAt = EditorApplication.timeSinceStartup + Mathf.Max(0.25f, S.autoSaveDelay);
    }

    void AutoSaveTick()
    {
        if (_autoSavePending && EditorApplication.timeSinceStartup >= _autoSaveDueAt)
            AutoSaveNow();
    }

    /// <summary>Write any pending auto-save right now (before a recompile / entering Play / deactivation).</summary>
    void FlushAutoSave()
    {
        if (_autoSavePending)
            AutoSaveNow();
    }

    void AutoSaveNow()
    {
        _autoSavePending = false;

        TerrainTool tool = Tool;
        if (tool == null || !S.autoSave || !HasAnyChunk(tool))
            return;

        string path = ResolveAutoSavePath(tool);
        try
        {
            // path is project-relative; Unity's working directory is the project root.
            File.WriteAllBytes(path, tool.SerializeData());

            AssetDatabase.ImportAsset(path);
            var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(path);

            // Keep it wired as Startup Data so a reload/Play restores this exact terrain.
            if (ta != null && tool.startupData != ta)
            {
                var so = new SerializedObject(tool);
                so.FindProperty("startupData").objectReferenceValue = ta;
                so.ApplyModifiedProperties();
            }
        }
        catch (System.Exception e)
        {
            // Never let an auto-save failure break tool deactivation / a recompile flush.
            Debug.LogWarning($"[TerrainTool] Auto-save skipped: {e.Message}");
        }
    }

    /// <summary>Reuse the assigned startupData's .bytes path if there is one; otherwise a stable default next to the terrain data.</summary>
    static string ResolveAutoSavePath(TerrainTool tool)
    {
        if (tool.startupData != null)
        {
            string p = AssetDatabase.GetAssetPath(tool.startupData);
            if (!string.IsNullOrEmpty(p) && p.EndsWith(".bytes"))
                return p;
        }

        string folder = "Assets/OnepenTerrainTool/Autosave";
        if (!AssetDatabase.IsValidFolder(folder))
            folder = EnsureFolder(folder);
        string name = string.IsNullOrEmpty(tool.name) ? "Terrain" : tool.name;
        return $"{folder}/{name}_AutoSave.bytes";
    }

    /// <summary>Create a project-relative folder (and any missing parents), returning the path, or "Assets" if creation fails.</summary>
    public static string EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
            return folder;

        string[] parts = folder.Split('/');
        if (parts.Length == 0 || parts[0] != "Assets")
            return "Assets";

        string current = "Assets";
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
        return AssetDatabase.IsValidFolder(folder) ? folder : "Assets";
    }

    // ---- Settings overlay --------------------------------------------------

    void DrawSettingsOverlay()
    {
        if (!S.showSceneOverlay)
        {
            _overlayRect = Rect.zero; // hidden — don't let a stale rect keep blocking sculpt clicks
            return;
        }

        Handles.BeginGUI();
        float h = S.mode == TerrainTool.BrushMode.Paint ? 318f : 196f;
        _overlayRect = new Rect(10, 10, 272, h);
        GUILayout.BeginArea(_overlayRect, GUIContent.none, EditorStyles.helpBox);

        EditorGUILayout.LabelField("Terrain Sculpt", EditorStyles.boldLabel);
        DrawBrushSettings(Tool, allowTextureEdit: false); // the scene overlay previews textures; assignment lives in the window

        GUILayout.EndArea();
        Handles.EndGUI();
    }

    /// <summary>
    /// Draw the brush controls against the shared <see cref="TerrainSculptSettings"/>,
    /// styled after Unity's own Terrain inspector (a mode toolbar, brush sliders and a
    /// texture thumbnail palette). Reused by the scene overlay and the
    /// <see cref="TerrainToolWindow"/>/inspector so every panel looks and behaves
    /// identically and stays in sync. Pass the <paramref name="tool"/> so the palette can
    /// show its paint textures; set <paramref name="allowTextureEdit"/> to also expose the
    /// per-slot assignment fields and the texture-array bake button (window/inspector only).
    /// </summary>
    public static void DrawBrushSettings(TerrainTool tool = null, bool allowTextureEdit = false)
    {
        TerrainSculptSettings s = S;
        EditorGUI.BeginChangeCheck();

        // Mode toolbar (Sculpt / Paint) — the big icon row at the top of Unity's terrain tools.
        s.mode = (TerrainTool.BrushMode)GUILayout.Toolbar((int)s.mode, ModeTabs(), GUILayout.Height(30));
        EditorGUILayout.Space(4);

        s.applyMode = (TerrainSculptSettings.ApplyMode)EditorGUILayout.EnumPopup(
            new GUIContent("Apply", "OnRelease: preview the path, apply on release.  OnMove: apply as you drag.  WhileHeld: apply continuously while held."),
            s.applyMode);
        s.radius = EditorGUILayout.Slider(new GUIContent("Brush Size", "World radius of the brush."), s.radius, 0.1f, 50f);

        if (s.mode == TerrainTool.BrushMode.Paint)
        {
            s.paintStrength = EditorGUILayout.Slider(new GUIContent("Opacity", "How fast the texture builds toward full weight."), s.paintStrength, 0.1f, 20f);
            s.paintEdgeBlend = EditorGUILayout.Slider(new GUIContent("Edge Blend", "Fraction of the rim that cross-fades into surrounding textures."), s.paintEdgeBlend, 0f, 1f);
            EditorGUILayout.Space(4);
            DrawTexturePalette(tool, allowTextureEdit);
        }
        else
        {
            s.strength = EditorGUILayout.Slider(new GUIContent("Opacity", "How strongly each dab raises/lowers the surface."), s.strength, 0.1f, 50f);
            EditorGUILayout.LabelField("LMB build · Shift+LMB carve", EditorStyles.miniLabel);
        }

        if (EditorGUI.EndChangeCheck())
        {
            s.Persist();
            SceneView.RepaintAll(); // reflect radius/mode changes in the brush gizmo immediately
        }
    }

    // ---- Texture palette (Unity-terrain-style layer grid) ------------------

    /// <summary>
    /// Draw the paintable textures as a grid of live thumbnails, like the "Terrain Layers"
    /// section of Unity's terrain inspector: click a swatch to select the slot that
    /// painting writes into, with the current one highlighted. When
    /// <paramref name="allowEdit"/> is set, per-slot object fields and the array-bake button
    /// are shown too so textures can be assigned right here.
    /// </summary>
    static void DrawTexturePalette(TerrainTool tool, bool allowEdit)
    {
        TerrainSculptSettings s = S;
        int slots = MatSplat.SlotCount;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Textures", EditorStyles.boldLabel);

            const float cellH = 60f;
            const float pad = 3f;
            Rect row = GUILayoutUtility.GetRect(10, cellH, GUILayout.ExpandWidth(true));
            float cw = row.width / slots;

            for (int i = 0; i < slots; i++)
            {
                var cell = new Rect(row.x + i * cw + pad, row.y + pad, cw - 2 * pad, cellH - 2 * pad);
                Texture2D tex = tool != null && tool.paintTextures != null && i < tool.paintTextures.Length
                    ? tool.paintTextures[i]
                    : null;

                if (Event.current.type == EventType.MouseDown && Event.current.button == 0 &&
                    cell.Contains(Event.current.mousePosition))
                {
                    s.selectedMaterial = i;
                    s.Persist();
                    SceneView.RepaintAll();
                    Event.current.Use();
                }

                EditorGUI.DrawRect(cell, new Color(0.17f, 0.17f, 0.17f, 1f)); // backing so empty slots read as wells
                if (tex != null)
                    GUI.DrawTexture(cell, tex, ScaleMode.ScaleAndCrop, false);
                else
                    GUI.Label(cell, "Empty", EmptySlotStyle());

                GUI.Label(new Rect(cell.x + 3, cell.y + 1, cell.width - 6, 15), (i + 1).ToString(), BadgeStyle());

                bool selected = s.selectedMaterial == i;
                DrawBorder(cell, selected ? 2f : 1f,
                    selected ? new Color(0.27f, 0.55f, 0.95f) : new Color(0f, 0f, 0f, 0.6f));
            }

            EditorGUILayout.LabelField($"Selected: Texture {s.selectedMaterial + 1}", EditorStyles.miniBoldLabel);

            if (!allowEdit || tool == null)
                return;

            EditorGUILayout.Space(2);
            var so = new SerializedObject(tool);
            SerializedProperty texProp = so.FindProperty("paintTextures");
            SerializedProperty tileProp = so.FindProperty("paintTextureTiling");

            bool texturesChanged = false;
            bool tilingChanged = false;

            for (int i = 0; i < slots; i++)
            {
                Texture2D curTex = tool.paintTextures != null && i < tool.paintTextures.Length ? tool.paintTextures[i] : null;
                Vector2 curTile = tool.paintTextureTiling != null && i < tool.paintTextureTiling.Length ? tool.paintTextureTiling[i] : Vector2.one;

                EditorGUI.BeginChangeCheck();
                var nextTex = (Texture2D)EditorGUILayout.ObjectField(new GUIContent($"Texture {i + 1}"), curTex, typeof(Texture2D), false);
                if (EditorGUI.EndChangeCheck())
                {
                    if (texProp.arraySize < slots)
                        texProp.arraySize = slots;
                    texProp.GetArrayElementAtIndex(i).objectReferenceValue = nextTex;
                    texturesChanged = true;
                }

                EditorGUI.indentLevel++;
                EditorGUI.BeginChangeCheck();
                Vector2 nextTile = EditorGUILayout.Vector2Field(new GUIContent("Tiling", "World-space repeat multiplier for this texture (1,1 = default)."), curTile);
                if (EditorGUI.EndChangeCheck())
                {
                    EnsureTilingSize(tileProp, slots);
                    tileProp.GetArrayElementAtIndex(i).vector2Value = nextTile;
                    tilingChanged = true;
                }
                EditorGUI.indentLevel--;
            }

            if (texturesChanged || tilingChanged)
            {
                so.ApplyModifiedProperties();
                if (texturesChanged)
                    TerrainToolActions.AutoBakeTextureArray(tool); // re-bake + rebind (EnsureInitialized also re-applies tiling)
                else
                    tool.ApplyTextureTiling(); // tiling only — no need to rebuild the texture array
                SceneView.RepaintAll();
            }

            EditorGUILayout.LabelField("Textures auto-bake into the Texture Array on change.", EditorStyles.miniLabel);

            EditorGUILayout.Space(2);
            if (GUILayout.Button("Re-bake Texture Array As…"))
                TerrainToolActions.BuildAndSaveTextureArray(tool);
        }
    }

    // ---- Small UI helpers --------------------------------------------------

    static GUIContent[] _modeTabs;
    static GUIStyle _emptySlotStyle, _badgeStyle;

    /// <summary>Icon+label tabs for the Sculpt/Paint mode toolbar, reusing Unity's own terrain-tool icons.</summary>
    static GUIContent[] ModeTabs()
    {
        if (_modeTabs == null)
        {
            Texture sculpt = EditorGUIUtility.IconContent("TerrainInspector.TerrainToolSculpt").image;
            Texture paint = EditorGUIUtility.IconContent("TerrainInspector.TerrainToolSplat").image;
            _modeTabs = new[]
            {
                new GUIContent(" Sculpt", sculpt, "Reshape the terrain. LMB build · Shift+LMB carve."),
                new GUIContent(" Paint",  paint,  "Blend a texture onto the surface without changing its shape."),
            };
        }
        return _modeTabs;
    }

    static GUIStyle EmptySlotStyle() => _emptySlotStyle ??= new GUIStyle(EditorStyles.miniLabel)
    {
        alignment = TextAnchor.MiddleCenter,
        normal = { textColor = new Color(0.6f, 0.6f, 0.6f) },
    };

    static GUIStyle BadgeStyle() => _badgeStyle ??= new GUIStyle(EditorStyles.miniBoldLabel)
    {
        normal = { textColor = Color.white },
    };

    /// <summary>Grow the tiling array to <paramref name="slots"/> entries, defaulting any new ones to (1,1).</summary>
    static void EnsureTilingSize(SerializedProperty tileProp, int slots)
    {
        int old = tileProp.arraySize;
        if (old >= slots)
            return;
        tileProp.arraySize = slots;
        for (int k = old; k < slots; k++)
            tileProp.GetArrayElementAtIndex(k).vector2Value = Vector2.one;
    }

    /// <summary>Draw a <paramref name="thickness"/>-px outline inside <paramref name="r"/> in <paramref name="c"/>.</summary>
    static void DrawBorder(Rect r, float thickness, Color c)
    {
        EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, thickness), c);
        EditorGUI.DrawRect(new Rect(r.x, r.yMax - thickness, r.width, thickness), c);
        EditorGUI.DrawRect(new Rect(r.x, r.y, thickness, r.height), c);
        EditorGUI.DrawRect(new Rect(r.xMax - thickness, r.y, thickness, r.height), c);
    }
}
