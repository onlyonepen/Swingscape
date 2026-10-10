using UnityEngine;

/// <summary>
/// Immutable snapshot of the terrain's grid configuration plus every
/// base ⇄ Lod ⇄ world conversion the rest of the tool relies on. Built from the
/// (already validated) inspector fields on <see cref="TerrainTool"/> via
/// <see cref="TerrainTool.Grid"/> and handed to chunks, sculpting and save/load,
/// so the lattice math lives in exactly one place instead of scattered
/// <c>1 &lt;&lt; lod</c> / <c>chunkSize &gt;&gt; lod</c> expressions that have to
/// agree by hand.
///
/// The invariants this type assumes (power-of-two <see cref="ChunkSize"/>,
/// <c>ChunkSize &gt;&gt; MaxLod &gt;= 1</c>, positive <see cref="CellSize"/>) are
/// guaranteed by <see cref="TerrainTool.ValidateConfig"/>; the constructor clamps
/// defensively so a Grid built from raw data can never divide by zero.
/// </summary>
public readonly struct TerrainGrid
{
    /// <summary>
    /// How far empty space sits above <see cref="IsoLevel"/>. Air density is
    /// derived from the iso level (rather than a fixed constant), so no matter
    /// what iso level you pick, untouched/new chunks always read as air instead
    /// of silently flipping to solid.
    /// </summary>
    public const float AirPadding = 1f;

    /// <summary>Base (Lod 0 / finest) cells per chunk axis. Always a power of two so every Lod divides it evenly.</summary>
    public readonly int ChunkSize;
    /// <summary>Base (Lod 0 / finest) world spacing between samples. Always &gt; 0.</summary>
    public readonly float CellSize;
    /// <summary>Coarsest allowed Lod. <c>ChunkSize &gt;&gt; MaxLod</c> is guaranteed &gt;= 1.</summary>
    public readonly int MaxLod;
    /// <summary>Surface threshold: a sample is solid where <c>density &lt; IsoLevel</c>.</summary>
    public readonly float IsoLevel;

    public TerrainGrid(int chunkSize, float cellSize, int maxLod, float isoLevel)
    {
        ChunkSize = Mathf.Max(Mathf.ClosestPowerOfTwo(Mathf.Max(chunkSize, 1)), 1);
        CellSize = Mathf.Max(cellSize, MinCellSize);
        MaxLod = Mathf.Clamp(maxLod, 0, MaxLodFor(ChunkSize));
        IsoLevel = isoLevel;
    }

    /// <summary>Smallest world spacing we allow, so no path can divide by (near-)zero.</summary>
    public const float MinCellSize = 1e-3f;

    /// <summary>Coarsest Lod a given base chunk size can support while keeping at least one cell per axis.</summary>
    public static int MaxLodFor(int chunkSize)
    {
        chunkSize = Mathf.Max(chunkSize, 1);
        int lod = 0;
        while ((chunkSize >> (lod + 1)) >= 1) // integer-exact: avoids Log2 landing just under a whole number
            lod++;
        return lod;
    }

    /// <summary>Density value for empty space — always safely above <see cref="IsoLevel"/>.</summary>
    public float AirDensity => IsoLevel + AirPadding;

    /// <summary>World size of one chunk along an axis (identical at every Lod — only the sample count changes).</summary>
    public float ChunkWorldSize => ChunkSize * CellSize;

    /// <summary>Cells per axis a chunk at <paramref name="lod"/> stores (samples = this + 1).</summary>
    public int CellsAtLod(int lod) => ChunkSize >> lod;
    /// <summary>Samples per axis a chunk at <paramref name="lod"/> stores.</summary>
    public int SamplesAtLod(int lod) => (ChunkSize >> lod) + 1;
    /// <summary>Base-lattice step between two adjacent samples of a chunk at <paramref name="lod"/>.</summary>
    public int StrideAtLod(int lod) => 1 << lod;
    /// <summary>World spacing between two adjacent samples of a chunk at <paramref name="lod"/>.</summary>
    public float SpacingAtLod(int lod) => CellSize * (1 << lod);

    /// <summary>Nearest valid Lod (clamped to [0, MaxLod]) for a desired world-space cell size.</summary>
    public int LodForCellSize(float desiredCellSize)
    {
        if (desiredCellSize <= 0f)
            return MaxLod;

        float ratio = Mathf.Max(desiredCellSize / CellSize, 1f);
        return Mathf.Clamp(Mathf.RoundToInt(Mathf.Log(ratio, 2f)), 0, MaxLod);
    }

    /// <summary>World-sample lattice coordinate (base / Lod-0 units) of a chunk-relative base index.</summary>
    public Vector3Int SampleWorld(Vector3Int coord, int baseLx, int baseLy, int baseLz)
        => new Vector3Int(
            coord.x * ChunkSize + baseLx,
            coord.y * ChunkSize + baseLy,
            coord.z * ChunkSize + baseLz);

    /// <summary>Manager-local position of a chunk's origin.</summary>
    public Vector3 ChunkLocalPosition(Vector3Int coord) => (Vector3)coord * ChunkWorldSize;

    /// <summary>
    /// Convert a base-lattice local index to a chunk's own (possibly coarser)
    /// local index for a chunk at <paramref name="lod"/>, or fail if that chunk
    /// doesn't store this exact sample.
    /// </summary>
    public bool TryLocalIndex(int lod, Vector3Int localBase, out Vector3Int local)
    {
        int stride = 1 << lod;
        if (localBase.x % stride != 0 || localBase.y % stride != 0 || localBase.z % stride != 0)
        {
            local = default;
            return false;
        }

        local = new Vector3Int(localBase.x / stride, localBase.y / stride, localBase.z / stride);
        return true;
    }
}
