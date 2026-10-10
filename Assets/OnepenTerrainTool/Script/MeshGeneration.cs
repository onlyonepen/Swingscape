using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PART 2 — Mesh Generation.
///
/// Bridges the raw algorithm (<see cref="CubeMarching"/>) and Unity. It walks a
/// 3D scalar (density) field cell by cell, asks CubeMarching to polygonise each
/// cell, optionally welds shared vertices for smooth shading, and fills a
/// Unity <see cref="Mesh"/>.
/// </summary>
public static class MeshGeneration
{
    // Reused scratch buffers so repeated regeneration doesn't allocate garbage.
    static readonly List<Vector3> s_Vertices = new List<Vector3>();
    static readonly List<int> s_Triangles = new List<int>();
    static readonly List<MatSplat> s_VertSplats = new List<MatSplat>();
    static readonly Vector3[] s_CornerPos = new Vector3[8];
    static readonly float[] s_CornerVal = new float[8];
    static readonly MatSplat[] s_CornerSplat = new MatSplat[8];
    static readonly List<Color> s_Colors = new List<Color>();

    /// <summary>
    /// Build (or rebuild) a mesh from a density field, carrying per-sample texture
    /// weights onto the vertices (the four fixed-slice weights → vertex color) for
    /// the texture-splat shader.
    /// </summary>
    /// <param name="density">
    /// Sample grid of size [nx, ny, nz]. The surface passes where the field
    /// crosses <paramref name="isoLevel"/>.
    /// </param>
    /// <param name="material">Per-sample material splats, same dimensions as <paramref name="density"/>.</param>
    /// <param name="isoLevel">Surface threshold. Corners below it are "inside".</param>
    /// <param name="cellSize">World size of one grid cell.</param>
    /// <param name="weld">Merge coincident vertices for smooth normals.</param>
    /// <param name="mesh">Existing mesh to overwrite, or null to create one.</param>
    public static Mesh Generate(float[,,] density, MatSplat[,,] material, float isoLevel, float cellSize, bool weld, Mesh mesh = null)
    {
        if (mesh == null)
        {
            mesh = new Mesh { name = "MarchingCubesMesh" };
        }
        else
        {
            mesh.Clear();
        }

        s_Vertices.Clear();
        s_Triangles.Clear();
        s_VertSplats.Clear();

        int nx = density.GetLength(0);
        int ny = density.GetLength(1);
        int nz = density.GetLength(2);

        // One cube per cell; cells span from a sample to its +1 neighbour.
        for (int x = 0; x < nx - 1; x++)
        for (int y = 0; y < ny - 1; y++)
        for (int z = 0; z < nz - 1; z++)
        {
            for (int c = 0; c < 8; c++)
            {
                Vector3Int o = CubeMarching.CornerOffsets[c];
                int cx = x + o.x;
                int cy = y + o.y;
                int cz = z + o.z;

                s_CornerPos[c] = new Vector3(cx, cy, cz) * cellSize;
                s_CornerVal[c] = density[cx, cy, cz];
                s_CornerSplat[c] = material[cx, cy, cz];
            }

            CubeMarching.PolygoniseCube(s_CornerPos, s_CornerVal, s_CornerSplat, isoLevel, s_Vertices, s_Triangles, s_VertSplats);
        }

        if (weld)
            Weld(s_Vertices, s_Triangles, s_VertSplats);

        // Marching cubes emits zero-area triangles wherever the surface grazes a
        // corner; PhysX fails to cook ("cleaning the mesh failed") on them, so drop
        // them before the mesh reaches the collider.
        RemoveDegenerateTriangles(s_Vertices, s_Triangles);

        // Pack the per-vertex splats into the vertex-color stream the shader reads.
        s_Colors.Clear();
        for (int i = 0; i < s_VertSplats.Count; i++)
        {
            s_VertSplats[i].WriteAttributes(out Color weights);
            s_Colors.Add(weights);
        }

        // A quad terrain can easily exceed 65k verts, so use a 32-bit index buffer.
        mesh.indexFormat = s_Vertices.Count > 65000
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;

        mesh.SetVertices(s_Vertices);
        mesh.SetColors(s_Colors);
        mesh.SetTriangles(s_Triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    /// <summary>
    /// Collapse duplicate vertices (produced because neighbouring cubes generate
    /// their own copy of a shared edge vertex) so that RecalculateNormals yields
    /// smooth shading across cell boundaries.
    /// </summary>
    /// <summary>
    /// Drop triangles with (near-)zero area. Two of a triangle's interpolated
    /// vertices coincide whenever the iso-surface passes through a cube corner,
    /// producing degenerate triangles that render as nothing but make PhysX's mesh
    /// cooker fail. The area test catches both coincident and collinear cases and
    /// doesn't depend on welding.
    /// </summary>
    static void RemoveDegenerateTriangles(List<Vector3> vertices, List<int> triangles)
    {
        int write = 0;
        for (int i = 0; i < triangles.Count; i += 3)
        {
            int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
            Vector3 ab = vertices[b] - vertices[a];
            Vector3 ac = vertices[c] - vertices[a];
            if (Vector3.Cross(ab, ac).sqrMagnitude <= 1e-14f)
                continue; // (2*area)^2 ~ 0 -> degenerate

            triangles[write++] = a;
            triangles[write++] = b;
            triangles[write++] = c;
        }
        triangles.RemoveRange(write, triangles.Count - write);
    }

    static void Weld(List<Vector3> vertices, List<int> triangles, List<MatSplat> splats)
    {
        var remap = new Dictionary<Vector3Int, int>(vertices.Count);
        var unique = new List<Vector3>(vertices.Count);
        var uniqueSplats = new List<MatSplat>(vertices.Count);
        var indexOf = new int[vertices.Count];

        const float quantize = 1000f; // ~1e-3 world units of merge tolerance

        for (int i = 0; i < vertices.Count; i++)
        {
            Vector3 v = vertices[i];
            var key = new Vector3Int(
                Mathf.RoundToInt(v.x * quantize),
                Mathf.RoundToInt(v.y * quantize),
                Mathf.RoundToInt(v.z * quantize));

            if (!remap.TryGetValue(key, out int idx))
            {
                idx = unique.Count;
                remap.Add(key, idx);
                unique.Add(v);
                // Coincident vertices sit on the same edge (same two corners), so
                // their interpolated splat is identical — keeping the first is exact.
                uniqueSplats.Add(splats[i]);
            }

            indexOf[i] = idx;
        }

        for (int i = 0; i < triangles.Count; i++)
            triangles[i] = indexOf[triangles[i]];

        vertices.Clear();
        vertices.AddRange(unique);
        splats.Clear();
        splats.AddRange(uniqueSplats);
    }
}
