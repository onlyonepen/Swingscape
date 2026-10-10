using UnityEditor;
using UnityEngine;
#if FBX_EXPORTER_PRESENT
using UnityEditor.Formats.Fbx.Exporter;
#endif

/// <summary>
/// Editor-only helpers to bake the live, sculpted terrain (all chunks) into a
/// single reusable asset: either a Unity <c>.asset</c> mesh or an <c>.fbx</c>
/// model. Because the terrain is generated at runtime, exporting must happen
/// while in Play mode.
/// </summary>
public static class TerrainMeshExporter
{
    /// <summary>Save the combined terrain as a reusable Unity mesh asset (.asset).</summary>
    public static void SaveMeshAsset(TerrainTool tool)
    {
        Mesh mesh = tool.CombineToMesh();
        if (mesh == null)
        {
            Debug.LogWarning("[TerrainMeshExporter] No terrain geometry to save.");
            return;
        }

        string path = EditorUtility.SaveFilePanelInProject(
            "Save Terrain Mesh", "TerrainMesh", "asset", "Choose where to save the mesh asset.");
        if (string.IsNullOrEmpty(path))
            return;

        AssetDatabase.CreateAsset(mesh, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Object saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        EditorGUIUtility.PingObject(saved);
        Debug.Log($"[TerrainMeshExporter] Saved reusable mesh asset to {path}");
    }

    /// <summary>Export the combined terrain as an FBX model asset.</summary>
    public static void ExportFbx(TerrainTool tool)
    {
#if FBX_EXPORTER_PRESENT
        Mesh mesh = tool.CombineToMesh();
        if (mesh == null)
        {
            Debug.LogWarning("[TerrainMeshExporter] No terrain geometry to export.");
            return;
        }

        string path = EditorUtility.SaveFilePanelInProject(
            "Export Terrain FBX", "TerrainMesh", "fbx", "Choose where to export the FBX.");
        if (string.IsNullOrEmpty(path))
            return;

        // The FBX exporter works on a GameObject hierarchy, so wrap the mesh in
        // a temporary object, export it, then throw the object away.
        var temp = new GameObject("TerrainExport");
        try
        {
            temp.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = temp.AddComponent<MeshRenderer>();
            mr.sharedMaterial = tool.chunkMaterial != null
                ? tool.chunkMaterial
                : new Material(Shader.Find("Standard"));

            string exported = ModelExporter.ExportObject(path, temp);
            if (!string.IsNullOrEmpty(exported))
            {
                AssetDatabase.Refresh();
                Object asset = AssetDatabase.LoadMainAssetAtPath(exported);
                EditorGUIUtility.PingObject(asset);
                Debug.Log($"[TerrainMeshExporter] Exported terrain FBX to {exported}");
            }
            else
            {
                Debug.LogWarning("[TerrainMeshExporter] FBX export returned no path.");
            }
        }
        finally
        {
            Object.DestroyImmediate(temp);
        }
#else
        Debug.LogWarning(
            "[TerrainMeshExporter] FBX Exporter package (com.unity.formats.fbx) is not installed. " +
            "Install it via the Package Manager, then try again.");
#endif
    }
}
