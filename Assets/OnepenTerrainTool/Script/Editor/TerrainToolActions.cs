using System.IO;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

/// <summary>
/// Shared edit-mode operations and the common control panel for a
/// <see cref="TerrainTool"/>, so the inspector (<see cref="TerrainToolEditor"/>) and
/// the dedicated <see cref="TerrainToolWindow"/> expose exactly the same buttons and
/// behave identically. All methods are no-ops / disabled in Play mode where a runtime
/// brush drives the terrain instead.
/// </summary>
public static class TerrainToolActions
{
    // Foldout state for the collapsible sections, laid out like Unity's terrain inspector:
    // the live brush/paint tools sit at the top, setup and export tuck away below.
    static bool _setupFoldout = true;
    static bool _configFoldout;
    static bool _exportFoldout;

    /// <summary>
    /// Draw the full edit-mode control panel (scene-editing toggle, brush, textures, setup,
    /// export) for <paramref name="tool"/>, styled after Unity's own Terrain inspector.
    /// Shared by the inspector and the tool window.
    /// </summary>
    public static void DrawControlPanel(TerrainTool tool)
    {
        if (tool == null)
        {
            EditorGUILayout.HelpBox("No TerrainTool selected.", MessageType.Info);
            return;
        }

        DrawSceneEditToggle(tool);
        TerrainSculptTool.DrawBrushSettings(tool, allowTextureEdit: true); // mode toolbar + brush + texture palette, shared with the overlay

        EditorGUILayout.Space(8);
        DrawSetupFoldout(tool);
        DrawConfigFoldout(tool);
        DrawExportFoldout(tool);
    }

    /// <summary>The prominent "edit in the scene view" toggle — the equivalent of selecting a Unity terrain tool.</summary>
    static void DrawSceneEditToggle(TerrainTool tool)
    {
        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            bool active = ToolManager.activeToolType == typeof(TerrainSculptTool);
            Color prev = GUI.backgroundColor;
            if (active)
                GUI.backgroundColor = new Color(0.45f, 0.85f, 0.5f);

            if (GUILayout.Button(active ? "● Editing in Scene — click to stop" : "Edit in Scene View", GUILayout.Height(26)))
            {
                if (active)
                {
                    ToolManager.RestorePreviousPersistentTool();
                }
                else
                {
                    EnsureMaterialAsset(tool);                    // load a saved terrain material in before editing
                    TerrainSculptSettings.instance.PushTo(tool); // make sure the grid/material are valid before the scene tool drives it
                    ToolManager.SetActiveTool<TerrainSculptTool>();
                }
            }
            GUI.backgroundColor = prev;
        }

        EditorGUILayout.LabelField("Drag over the terrain in the Scene view to edit. Ctrl+Z to undo.", EditorStyles.miniLabel);

        TerrainSculptSettings s = TerrainSculptSettings.instance;
        EditorGUI.BeginChangeCheck();
        s.showSceneOverlay = EditorGUILayout.ToggleLeft(
            new GUIContent("Show Scene Overlay", "Show the brush settings box in the Scene view while editing."),
            s.showSceneOverlay);
        s.autoSave = EditorGUILayout.ToggleLeft(
            new GUIContent("Auto-save Edits", "Persist the terrain to a .bytes asset shortly after you stop editing, " +
                                              "and keep it assigned to Startup Data so recompiles/Play restore it."),
            s.autoSave);
        if (s.autoSave)
            s.autoSaveDelay = EditorGUILayout.Slider(new GUIContent("Auto-save Delay", "Seconds of idle before a pending auto-save is written."), s.autoSaveDelay, 0.5f, 30f);
        if (EditorGUI.EndChangeCheck())
        {
            s.Persist();
            SceneView.RepaintAll();
        }

        EditorGUILayout.Space(4);
    }

    static void DrawSetupFoldout(TerrainTool tool)
    {
        _setupFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(_setupFoldout, "Terrain Setup");
        if (_setupFoldout)
        {
            EditorGUILayout.HelpBox(
                "Generate the starting terrain, then edit it in the Scene view. The terrain is a " +
                "transient editor session — use Export ▸ Save Terrain Data to keep it.", MessageType.Info);

            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                if (GUILayout.Button("Generate / Load Starting Terrain"))
                {
                    EnsureMaterialAsset(tool);                    // make sure a saved terrain material is loaded in first
                    TerrainSculptSettings.instance.PushTo(tool); // apply config, then build
                    tool.GenerateOrLoad();
                    SceneView.RepaintAll();
                }
                if (GUILayout.Button("Generate Fresh Slab (ignore Startup Data)"))
                {
                    EnsureMaterialAsset(tool);
                    TerrainSculptSettings.instance.PushTo(tool);
                    tool.GenerateFreshSlab();                     // clean canvas, ignoring any saved (possibly damaged) data
                    SceneView.RepaintAll();
                }
                if (GUILayout.Button("Clear Terrain"))
                {
                    tool.ClearChunks();
                    SceneView.RepaintAll();
                }
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    /// <summary>
    /// Grid resolution + LOD + initial-slab config, grouped so the link between cell size,
    /// chunk size and LOD is legible, with live hints showing what each Lod means in world
    /// units. Owned by the settings (not serialized on TerrainTool). Also surfaces the
    /// optional runtime view-distance LOD controller.
    /// </summary>
    static void DrawConfigFoldout(TerrainTool tool)
    {
        _configFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(_configFoldout, "Terrain Settings");
        if (_configFoldout)
        {
            TerrainSculptSettings s = TerrainSculptSettings.instance;
            EditorGUI.BeginChangeCheck();

            // A grid built from the current (self-clamping) values, so the hints match what the tool will actually use.
            var grid = new TerrainGrid(s.chunkSize, s.cellSize, s.maxLod, s.isoLevel);

            // --- Resolution: set before generating; the stored terrain uses this one cell size ---
            EditorGUILayout.LabelField("Resolution (set before generating)", EditorStyles.boldLabel);
            s.cellSize = EditorGUILayout.FloatField(new GUIContent("Cell Size", "World size of one voxel cell — the terrain's stored resolution. Determined at generation; change it then re-generate."), s.cellSize);
            s.chunkSize = EditorGUILayout.IntField(new GUIContent("Chunk Size", "Cells per axis in a chunk (snapped to a power of two)."), s.chunkSize);
            s.maxLod = EditorGUILayout.IntSlider(new GUIContent("Max Lod", "Coarsest Lod the chunk size supports — the ceiling for the view-distance LOD below."), s.maxLod, 0, TerrainGrid.MaxLodFor(grid.ChunkSize));
            Hint($"Stored at {grid.CellSize:0.###} u/cell   ·   chunk span {grid.ChunkWorldSize:0.##} u");

            // --- Surface ---
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Surface", EditorStyles.boldLabel);
            s.isoLevel = EditorGUILayout.FloatField(new GUIContent("Iso Level", "Surface threshold; solid where density < this."), s.isoLevel);
            s.smoothShading = EditorGUILayout.Toggle(new GUIContent("Smooth Shading", "Smooth (averaged) vs flat per-triangle normals."), s.smoothShading);

            // --- Initial terrain (slab) ---
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Initial Terrain (slab)", EditorStyles.boldLabel);
            s.initialChunks = EditorGUILayout.Vector3IntField(new GUIContent("Initial Chunks", "How many chunks the starting slab spans on each axis."), s.initialChunks);
            s.slabThickness = EditorGUILayout.FloatField(new GUIContent("Slab Thickness", "World height of the starting slab."), s.slabThickness);
            s.slabMargin = EditorGUILayout.FloatField(new GUIContent("Slab Margin", "Air gap kept around the slab so the surface stays capped."), s.slabMargin);

            if (EditorGUI.EndChangeCheck())
                s.Persist();

            // --- Level of Detail: a separate view-distance system — set the LOD count and each LOD's distance. ---
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Level of Detail (view distance)", EditorStyles.boldLabel);
            DrawRuntimeLodSection(tool);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    /// <summary>A dimmed, field-aligned hint line under a control.</summary>
    static void Hint(string text) => EditorGUILayout.LabelField(" ", text, EditorStyles.miniLabel);

    /// <summary>
    /// Inline editor for the optional <see cref="TerrainLodController"/>: add it, set how many LOD
    /// levels and the distance each begins at, or remove it — all without leaving the panel. This
    /// is a separate view-distance system: it only changes render resolution by camera distance,
    /// never the stored terrain.
    /// </summary>
    static void DrawRuntimeLodSection(TerrainTool tool)
    {
        var lodCtrl = tool.GetComponent<TerrainLodController>();
        if (lodCtrl == null)
        {
            EditorGUILayout.HelpBox(
                "Optional. Renders distant chunks at coarser Lods by camera distance — it never changes the stored terrain. Add it, then set the number of LODs and each LOD's distance.",
                MessageType.None);
            if (GUILayout.Button("Add Level of Detail"))
                Undo.AddComponent<TerrainLodController>(tool.gameObject);
            return;
        }

        var so = new SerializedObject(lodCtrl);
        so.Update();
        EditorGUI.BeginChangeCheck();

        EditorGUILayout.PropertyField(so.FindProperty("viewer"));

        // Amount of LODs, capped to the configured Max Lod (the coarsest the data can downsample to).
        int maxLevels = Mathf.Clamp(TerrainSculptSettings.instance.maxLod, 0, TerrainGrid.MaxLodFor(TerrainSculptSettings.instance.chunkSize)) + 1;
        SerializedProperty countProp = so.FindProperty("lodCount");
        EditorGUILayout.IntSlider(countProp, 1, maxLevels,
            new GUIContent("LOD Levels", "How many detail levels. LOD 0 = full detail; each higher level renders coarser."));

        // One distance per boundary between consecutive LODs. Draw from the array's CURRENT size so the
        // control count is stable within a frame (mutating it mid-layout causes IMGUI "mismatched layout"
        // errors). The controller's OnValidate resizes the array to lodCount-1 after Apply, so it settles
        // on the next repaint.
        SerializedProperty distProp = so.FindProperty("lodDistances");
        for (int i = 0; i < distProp.arraySize; i++)
            EditorGUILayout.PropertyField(distProp.GetArrayElementAtIndex(i),
                new GUIContent($"LOD {i} → {i + 1} at", $"Camera distance (world units) beyond which chunks render at LOD {i + 1}."));

        EditorGUILayout.PropertyField(so.FindProperty("updateInterval"));
        EditorGUILayout.PropertyField(so.FindProperty("hysteresis"));
        EditorGUILayout.PropertyField(so.FindProperty("balanceNeighbors"));

        if (EditorGUI.EndChangeCheck())
            so.ApplyModifiedProperties();

        if (GUILayout.Button("Remove Level of Detail"))
            Undo.DestroyObjectImmediate(lodCtrl);
    }

    static void DrawExportFoldout(TerrainTool tool)
    {
        _exportFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(_exportFoldout, "Export");
        if (_exportFoldout)
        {
            if (GUILayout.Button("Save Terrain Data (reusable, sculptable)"))
                SaveTerrainData(tool);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Mesh export (for use elsewhere, not sculptable)", EditorStyles.miniLabel);

            if (GUILayout.Button("Save Mesh as .asset"))
                TerrainMeshExporter.SaveMeshAsset(tool);

            if (GUILayout.Button("Save Mesh as FBX"))
                TerrainMeshExporter.ExportFbx(tool);

#if !FBX_EXPORTER_PRESENT
            EditorGUILayout.HelpBox(
                "FBX export needs the 'FBX Exporter' package (com.unity.formats.fbx). " +
                "Install it in the Package Manager to enable FBX output.", MessageType.Warning);
#endif
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    /// <summary>
    /// Bake the tool's paint textures into a Texture2DArray asset and assign it to
    /// the tool's Texture Array field, so the array isn't reassembled on every Play.
    /// </summary>
    public static void BuildAndSaveTextureArray(TerrainTool tool)
    {
        Texture2DArray array = TerrainTool.CreateTextureArray(tool.paintTextures);
        if (array == null)
        {
            Debug.LogError("[TerrainTool] No valid Paint Textures to build a texture array from.");
            return;
        }

        string path = EditorUtility.SaveFilePanelInProject(
            "Save Texture Array", "TerrainTextureArray", "asset",
            "Choose where to save the baked terrain texture array.");
        if (string.IsNullOrEmpty(path))
            return;

        AssetDatabase.CreateAsset(array, path);
        AssetDatabase.SaveAssets();

        var saved = AssetDatabase.LoadAssetAtPath<Texture2DArray>(path);
        var so = new SerializedObject(tool);
        so.FindProperty("textureArray").objectReferenceValue = saved;
        so.ApplyModifiedProperties();

        EditorGUIUtility.PingObject(saved);
        Debug.Log($"[TerrainTool] Saved texture array to {path} and assigned it to the Texture Array field.");
    }

    /// <summary>
    /// Re-bake the Texture2DArray from the current paint textures and keep it assigned, with
    /// no dialog — called automatically when a paint-texture slot changes so edits show up
    /// immediately in the scene. Overwrites the already-assigned array asset in place when
    /// there is one (so references stay valid); otherwise creates one at a default path next
    /// to the terrain tool and assigns it. A no-op when every slot is empty.
    /// </summary>
    public static void AutoBakeTextureArray(TerrainTool tool)
    {
        Texture2DArray array = TerrainTool.CreateTextureArray(tool.paintTextures);
        if (array == null)
        {
            // Nothing valid to bake (all slots cleared). Leave any existing asset untouched
            // but still rebind, so a now-empty list falls back to the runtime array.
            tool.EnsureInitialized();
            return;
        }

        string path = AssetDatabase.GetAssetPath(tool.textureArray);
        if (string.IsNullOrEmpty(path))
        {
            path = DefaultTextureArrayPath(tool);
            AssetDatabase.CreateAsset(array, path);
        }
        else
        {
            // Update the existing asset in place so prefabs/scenes keep referencing it.
            var existing = AssetDatabase.LoadAssetAtPath<Texture2DArray>(path);
            EditorUtility.CopySerialized(array, existing);
            Object.DestroyImmediate(array);
        }
        AssetDatabase.SaveAssets();

        var saved = AssetDatabase.LoadAssetAtPath<Texture2DArray>(path);
        var so = new SerializedObject(tool);
        so.FindProperty("textureArray").objectReferenceValue = saved;
        so.ApplyModifiedProperties();

        // Apply the baked array straight to the chunk material and persist it, so the
        // assignment survives reloads/Play without any manual step.
        if (tool.chunkMaterial != null)
        {
            tool.chunkMaterial.SetTexture("_TexArray", saved);
            EditorUtility.SetDirty(tool.chunkMaterial);
        }

        tool.EnsureInitialized(); // rebind + rebuild so the change shows in the scene
    }

    /// <summary>
    /// Make sure the tool has a persisted terrain material. If none is assigned (or only the
    /// runtime fallback from <see cref="TerrainTool.EnsureMaterial"/> is present), create a
    /// saved .mat asset from the Onepen/TerrainSplat shader next to the terrain data and
    /// assign it — so texture bakes and tiling edited on the material survive reloads/Play.
    /// </summary>
    public static void EnsureMaterialAsset(TerrainTool tool)
    {
        if (tool.chunkMaterial != null && AssetDatabase.Contains(tool.chunkMaterial))
            return; // already a saved material asset — nothing to do

        Shader shader = Shader.Find("Onepen/TerrainSplat");
        if (shader == null)
        {
            Debug.LogError("[TerrainTool] 'Onepen/TerrainSplat' shader not found; can't create a terrain material.");
            return;
        }

        // Reuse the runtime fallback instance if one was already made, else a fresh material.
        Material mat = tool.chunkMaterial != null ? tool.chunkMaterial : new Material(shader);

        string folder = "Assets/OnepenTerrainTool/TerrainData";
        if (!AssetDatabase.IsValidFolder(folder))
            folder = TerrainSculptTool.EnsureFolder(folder);
        string name = string.IsNullOrEmpty(tool.name) ? "Terrain" : tool.name;
        string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{name}_Material.mat");

        AssetDatabase.CreateAsset(mat, path);
        AssetDatabase.SaveAssets();

        var so = new SerializedObject(tool);
        so.FindProperty("chunkMaterial").objectReferenceValue = mat;
        so.ApplyModifiedProperties();

        tool.EnsureInitialized(); // bind the texture array + tiling onto the freshly saved material
        Debug.Log($"[TerrainTool] Created and assigned terrain material at {path}.");
    }

    /// <summary>Default location for an auto-baked texture array: alongside the terrain data, named after the tool.</summary>
    static string DefaultTextureArrayPath(TerrainTool tool)
    {
        string folder = "Assets/OnepenTerrainTool/TerrainData";
        if (!AssetDatabase.IsValidFolder(folder))
            folder = TerrainSculptTool.EnsureFolder(folder);
        string name = string.IsNullOrEmpty(tool.name) ? "Terrain" : tool.name;
        return AssetDatabase.GenerateUniqueAssetPath($"{folder}/{name}_TextureArray.asset");
    }

    /// <summary>
    /// Write the density (voxel) data to a .bytes file in the project. Import it
    /// as a TextAsset and drop it into TerrainTool.startupData to resume from it.
    /// </summary>
    public static void SaveTerrainData(TerrainTool tool)
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Save Terrain Data", "TerrainData", "bytes",
            "Choose where to save the sculptable terrain data.");
        if (string.IsNullOrEmpty(path))
            return;

        File.WriteAllBytes(path, tool.SerializeData());
        AssetDatabase.Refresh();

        // Save the terrain material alongside the data, with the same base name, so the
        // two always travel together (e.g. MyTerrain.bytes + MyTerrain.mat).
        string matPath = Path.ChangeExtension(path, ".mat");
        SaveMaterialAtPath(tool, matPath);

        var saved = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
        EditorGUIUtility.PingObject(saved);
        Debug.Log($"[TerrainTool] Saved sculptable terrain data to {path} and its material to {matPath}. " +
                  "Assign the .bytes to 'Startup Data' to load it as the starting terrain.");
    }

    /// <summary>
    /// Write the tool's current terrain material to <paramref name="path"/> and assign it as the
    /// tool's chunk material. Overwrites an existing material asset in place (so prefabs/scenes
    /// keep their reference) rather than creating a duplicate.
    /// </summary>
    static void SaveMaterialAtPath(TerrainTool tool, string path)
    {
        Shader shader = Shader.Find("Onepen/TerrainSplat");
        if (shader == null)
        {
            Debug.LogError("[TerrainTool] 'Onepen/TerrainSplat' shader not found; can't save a terrain material.");
            return;
        }

        // Snapshot the current material (texture array, tiling, etc.) so edits are persisted.
        Material mat = tool.chunkMaterial != null ? new Material(tool.chunkMaterial) : new Material(shader);

        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            // Keep the existing asset's identity; just copy the fresh settings onto it.
            EditorUtility.CopySerialized(mat, existing);
            Object.DestroyImmediate(mat);
            mat = existing;
        }
        else
        {
            AssetDatabase.CreateAsset(mat, path);
        }
        AssetDatabase.SaveAssets();

        var so = new SerializedObject(tool);
        so.FindProperty("chunkMaterial").objectReferenceValue = mat;
        so.ApplyModifiedProperties();

        tool.EnsureInitialized(); // rebind the texture array + tiling onto the saved material
    }
}
