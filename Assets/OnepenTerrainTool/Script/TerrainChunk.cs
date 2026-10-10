using UnityEngine;

/// <summary>
/// A single terrain chunk: one density matrix + its own GameObject with mesh
/// components. Because every chunk is a separate renderer, Unity frustum-culls
/// them individually — that's the whole point of chunking here.
///
/// A chunk's world-space footprint never changes, but its internal resolution
/// (<see cref="Lod"/>) can: a chunk of <see cref="ChunkSize"/> cells per axis
/// stores (ChunkSize+1) samples per axis, at <see cref="CellSize"/> spacing.
/// Adjacent chunks may only differ by one Lod level (see TerrainTool), and the
/// aligned samples they both happen to store are kept equal so their surfaces
/// line up wherever they can — keeping that invariant is the manager's job
/// (<see cref="TerrainTool"/>).
///
/// Storage vs. render resolution: <see cref="Lod"/> is the resolution the density
/// is STORED at (the finest it's been sculpted to — the source of truth, never
/// thrown away). <see cref="RenderLod"/> is the resolution the visible MESH is
/// built at, and may be coarser than the data (never finer) so distant chunks
/// render cheaply. <see cref="TerrainLodController"/> raises/lowers RenderLod with
/// camera distance; because the mesh is built from a downsampled *copy* of the
/// density, walking away and back restores full detail losslessly.
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class TerrainChunk : MonoBehaviour
{
    public Vector3Int Coord { get; private set; }
    public float[,,] Density { get; private set; }
    /// <summary>Per-sample material weights (texture painting), same dimensions as <see cref="Density"/>. Defaults to unpainted (renders as material 0).</summary>
    public MatSplat[,,] Material { get; private set; }

    /// <summary>Storage Lod. 0 = finest (base resolution). Chunk world footprint never changes with Lod — only its internal sample density does.</summary>
    public int Lod { get; private set; }
    /// <summary>Render Lod: resolution the visible mesh is built at. Always &gt;= <see cref="Lod"/> (a mesh coarser than the data is fine; finer is impossible).</summary>
    public int RenderLod { get; private set; }
    /// <summary>This chunk's own cells-per-axis at its current (storage) Lod (baseChunkSize >> Lod).</summary>
    public int ChunkSize { get; private set; }
    /// <summary>This chunk's own world-space sample spacing at its current (storage) Lod (baseCellSize * 2^Lod).</summary>
    public float CellSize { get; private set; }

    Mesh _mesh;
    MeshFilter _meshFilter;
    MeshCollider _meshCollider;

    /// <summary>Allocate the density matrix (sized for the given Lod) and cache components.</summary>
    public void Init(TerrainGrid grid, Vector3Int coord, int lod)
    {
        Coord = coord;
        Lod = lod;
        RenderLod = lod; // born rendering at full stored detail; the LOD controller coarsens it with distance
        ChunkSize = grid.CellsAtLod(lod);
        CellSize = grid.SpacingAtLod(lod);

        int n = ChunkSize + 1; // +1 shared border row
        Density = new float[n, n, n];
        Material = new MatSplat[n, n, n]; // all MatSplat.Empty (unpainted)

        if (!TryGetComponent(out _meshFilter)) _meshFilter = gameObject.AddComponent<MeshFilter>();
        if (!TryGetComponent(out _meshCollider)) _meshCollider = gameObject.AddComponent<MeshCollider>();
    }

    /// <summary>Swap in a resampled density field after a storage LOD change (see TerrainTool.RefineChunk/CoarsenChunk). Render Lod snaps back to full stored detail; the controller re-coarsens if the chunk is distant.</summary>
    public void ApplyResampled(float[,,] newDensity, MatSplat[,,] newMaterial, int newLod, int newChunkSize, float newCellSize)
    {
        Density = newDensity;
        Material = newMaterial;
        Lod = newLod;
        RenderLod = newLod;
        ChunkSize = newChunkSize;
        CellSize = newCellSize;
    }

    /// <summary>Set the resolution the mesh is drawn at (clamped by the manager to [Lod, maxLod]). Does not touch stored density.</summary>
    public void SetRenderLod(int renderLod) => RenderLod = renderLod;

    /// <summary>
    /// Rebuild the mesh at <see cref="RenderLod"/>. When RenderLod matches the
    /// storage Lod the stored density is meshed directly; when it's coarser the
    /// mesh is built from a downsampled *copy*, so the stored data (and thus full
    /// detail on a later refine) is preserved.
    /// </summary>
    public void Rebuild(TerrainGrid grid, bool smoothShading)
    {
        float[,,] field;
        MatSplat[,,] matField;
        float spacing;
        if (RenderLod <= Lod)
        {
            field = Density;      // render at full stored detail
            matField = Material;
            spacing = CellSize;
        }
        else
        {
            int renderCells = grid.CellsAtLod(RenderLod);
            field = LodResample.Coarsen(Density, ChunkSize, renderCells); // non-destructive downsample
            matField = LodResample.CoarsenMaterial(Material, ChunkSize, renderCells);
            spacing = grid.SpacingAtLod(RenderLod);
        }

        _mesh = MeshGeneration.Generate(field, matField, grid.IsoLevel, spacing, smoothShading, _mesh);
        _meshFilter.sharedMesh = _mesh;

        // A MeshCollider rejects (and warns about) a mesh with no non-degenerate triangle — an
        // all-air/all-solid chunk, or one whose only triangles are zero-area (e.g. a slab face that
        // lands exactly on a grid plane). The render mesh is fine with that; the collider isn't.
        _meshCollider.sharedMesh = IsCollidable(_mesh) ? _mesh : null;
    }

    /// <summary>
    /// True when a mesh is a valid collision mesh: at least one triangle, and real 2D+ extent
    /// (a point or a line means every triangle is degenerate). Allocation-free, so it's safe on
    /// the per-dab rebuild path.
    /// </summary>
    static bool IsCollidable(Mesh mesh)
    {
        if (mesh == null || mesh.vertexCount < 3 || mesh.subMeshCount == 0 || mesh.GetIndexCount(0) < 3)
            return false;

        // A non-degenerate triangle spans two dimensions, so a valid surface's bounds do too;
        // an all-degenerate mesh collapses to a line or point (<2 non-zero extents).
        Vector3 size = mesh.bounds.size;
        int spanned = (size.x > 1e-5f ? 1 : 0) + (size.y > 1e-5f ? 1 : 0) + (size.z > 1e-5f ? 1 : 0);
        return spanned >= 2;
    }
}
