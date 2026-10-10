using UnityEngine;

/// <summary>
/// Pure resampling helpers for changing a chunk's internal sample density in
/// place while its world-space footprint stays fixed. LOD ratios are always a
/// power of two, so coarsening is an exact point-pick (every coarse sample
/// coincides exactly with a fine one) and refining is the only direction that
/// needs interpolation.
/// </summary>
public static class LodResample
{
    /// <summary>Trilinear-upsample a coarser density field onto a finer grid of the same footprint.</summary>
    public static float[,,] Refine(float[,,] coarse, int oldChunkSize, int newChunkSize)
    {
        int newN = newChunkSize + 1;
        float ratio = (float)newChunkSize / oldChunkSize; // > 1
        var result = new float[newN, newN, newN];

        for (int x = 0; x < newN; x++)
        for (int y = 0; y < newN; y++)
        for (int z = 0; z < newN; z++)
            result[x, y, z] = SampleTrilinear(coarse, oldChunkSize, x / ratio, y / ratio, z / ratio);

        return result;
    }

    /// <summary>Exact point-pick downsample (ratio is always a power of two, so coarse samples coincide exactly with fine ones).</summary>
    public static float[,,] Coarsen(float[,,] fine, int oldChunkSize, int newChunkSize)
    {
        int newN = newChunkSize + 1;
        int step = oldChunkSize / newChunkSize;
        var result = new float[newN, newN, newN];

        for (int x = 0; x < newN; x++)
        for (int y = 0; y < newN; y++)
        for (int z = 0; z < newN; z++)
            result[x, y, z] = fine[x * step, y * step, z * step];

        return result;
    }

    /// <summary>Trilinear-upsample a coarser material field onto a finer grid (uses <see cref="MatSplat.Lerp"/> so differing layer sets merge correctly).</summary>
    public static MatSplat[,,] RefineMaterial(MatSplat[,,] coarse, int oldChunkSize, int newChunkSize)
    {
        int newN = newChunkSize + 1;
        float ratio = (float)newChunkSize / oldChunkSize; // > 1
        var result = new MatSplat[newN, newN, newN];

        for (int x = 0; x < newN; x++)
        for (int y = 0; y < newN; y++)
        for (int z = 0; z < newN; z++)
            result[x, y, z] = SampleTrilinearMat(coarse, oldChunkSize, x / ratio, y / ratio, z / ratio);

        return result;
    }

    /// <summary>Exact point-pick downsample of a material field (same power-of-two coincidence as <see cref="Coarsen"/>).</summary>
    public static MatSplat[,,] CoarsenMaterial(MatSplat[,,] fine, int oldChunkSize, int newChunkSize)
    {
        int newN = newChunkSize + 1;
        int step = oldChunkSize / newChunkSize;
        var result = new MatSplat[newN, newN, newN];

        for (int x = 0; x < newN; x++)
        for (int y = 0; y < newN; y++)
        for (int z = 0; z < newN; z++)
            result[x, y, z] = fine[x * step, y * step, z * step];

        return result;
    }

    static float SampleTrilinear(float[,,] field, int chunkSize, float x, float y, float z)
    {
        int n = chunkSize + 1;
        int x0 = Mathf.Clamp(Mathf.FloorToInt(x), 0, n - 1), x1 = Mathf.Min(x0 + 1, n - 1);
        int y0 = Mathf.Clamp(Mathf.FloorToInt(y), 0, n - 1), y1 = Mathf.Min(y0 + 1, n - 1);
        int z0 = Mathf.Clamp(Mathf.FloorToInt(z), 0, n - 1), z1 = Mathf.Min(z0 + 1, n - 1);
        float fx = x - x0, fy = y - y0, fz = z - z0;

        float c000 = field[x0, y0, z0], c100 = field[x1, y0, z0];
        float c010 = field[x0, y1, z0], c110 = field[x1, y1, z0];
        float c001 = field[x0, y0, z1], c101 = field[x1, y0, z1];
        float c011 = field[x0, y1, z1], c111 = field[x1, y1, z1];

        float c00 = Mathf.Lerp(c000, c100, fx), c10 = Mathf.Lerp(c010, c110, fx);
        float c01 = Mathf.Lerp(c001, c101, fx), c11 = Mathf.Lerp(c011, c111, fx);
        float c0 = Mathf.Lerp(c00, c10, fy), c1 = Mathf.Lerp(c01, c11, fy);
        return Mathf.Lerp(c0, c1, fz);
    }

    static MatSplat SampleTrilinearMat(MatSplat[,,] field, int chunkSize, float x, float y, float z)
    {
        int n = chunkSize + 1;
        int x0 = Mathf.Clamp(Mathf.FloorToInt(x), 0, n - 1), x1 = Mathf.Min(x0 + 1, n - 1);
        int y0 = Mathf.Clamp(Mathf.FloorToInt(y), 0, n - 1), y1 = Mathf.Min(y0 + 1, n - 1);
        int z0 = Mathf.Clamp(Mathf.FloorToInt(z), 0, n - 1), z1 = Mathf.Min(z0 + 1, n - 1);
        float fx = x - x0, fy = y - y0, fz = z - z0;

        MatSplat c00 = MatSplat.Lerp(field[x0, y0, z0], field[x1, y0, z0], fx);
        MatSplat c10 = MatSplat.Lerp(field[x0, y1, z0], field[x1, y1, z0], fx);
        MatSplat c01 = MatSplat.Lerp(field[x0, y0, z1], field[x1, y0, z1], fx);
        MatSplat c11 = MatSplat.Lerp(field[x0, y1, z1], field[x1, y1, z1], fx);

        MatSplat c0 = MatSplat.Lerp(c00, c10, fy), c1 = MatSplat.Lerp(c01, c11, fy);
        return MatSplat.Lerp(c0, c1, fz);
    }
}
