using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PART 4 — View-distance LOD.
///
/// Drives each chunk's RENDER Lod (mesh resolution) from its distance to the
/// camera: near chunks render at full stored detail, far chunks render a coarser
/// mesh (fewer verts). This is non-destructive — it only changes
/// <see cref="TerrainChunk.RenderLod"/>, never the stored density — so walking
/// away and back restores full detail losslessly, and sculpting is never eroded.
/// Neighbours are kept within one render-Lod level so any seam is a single step;
/// distance + fog make those far-away seams hard to notice.
///
/// It runs a periodic sweep (not every frame) that:
///   1. picks a desired render Lod per chunk from distance, with a hysteresis
///      dead-band so chunks parked near a band edge don't flip-flop;
///   2. optionally relaxes those targets so no two face-adjacent chunks differ by
///      more than one level (keeps any residual seam to a single step);
///   3. applies them via <see cref="TerrainTool.SetChunkRenderLod"/>, which only
///      rebuilds the chunks whose render Lod actually changed.
/// </summary>
[RequireComponent(typeof(TerrainTool))]
public class TerrainLodController : MonoBehaviour
{
    [Tooltip("Camera distance is measured from. Defaults to Camera.main at runtime if left empty.")]
    public Camera viewer;
    [Tooltip("How many LOD levels to use. LOD 0 = full stored detail; each higher level renders coarser.")]
    [Min(1)] public int lodCount = 4;
    [Tooltip("Distance (world units) at which each next LOD begins. Entry i is the boundary between LOD i and LOD i+1, " +
             "so there are (lodCount - 1) of them, in increasing order.")]
    public float[] lodDistances = { 20f, 40f, 60f };
    [Tooltip("Seconds between LOD re-evaluations (0 = every frame). Keeps the sweep off the per-frame hot path.")]
    public float updateInterval = 0.25f;
    [Tooltip("Distance dead-band a chunk must cross before it may switch Lod again, so chunks near a band edge don't flip-flop.")]
    public float hysteresis = 5f;
    [Tooltip("Keep face-adjacent chunks within one render-Lod level.")]
    public bool balanceNeighbors = true;

    static readonly Vector3Int[] FaceOffsets =
    {
        new Vector3Int(-1, 0, 0), new Vector3Int(1, 0, 0),
        new Vector3Int(0, -1, 0), new Vector3Int(0, 1, 0),
        new Vector3Int(0, 0, -1), new Vector3Int(0, 0, 1),
    };

    TerrainTool _terrain;
    float _timer;
    readonly Dictionary<Vector3Int, int> _target = new Dictionary<Vector3Int, int>();

    void Awake() => _terrain = GetComponent<TerrainTool>();

    void OnValidate()
    {
        lodCount = Mathf.Max(lodCount, 1);
        updateInterval = Mathf.Max(updateInterval, 0f);
        hysteresis = Mathf.Max(hysteresis, 0f);

        // Keep one boundary distance per LOD step, strictly increasing and positive.
        int need = Mathf.Max(0, lodCount - 1);
        if (lodDistances == null)
            lodDistances = new float[0];
        if (lodDistances.Length != need)
        {
            var resized = new float[need];
            for (int i = 0; i < need; i++)
                resized[i] = i < lodDistances.Length ? lodDistances[i] : (i > 0 ? resized[i - 1] + 20f : 20f);
            lodDistances = resized;
        }
        float min = 0f;
        for (int i = 0; i < lodDistances.Length; i++)
        {
            lodDistances[i] = Mathf.Max(lodDistances[i], min + 0.01f); // each band must be past the previous one
            min = lodDistances[i];
        }
    }

    void LateUpdate()
    {
        _timer -= Time.deltaTime;
        if (_timer > 0f)
            return;
        _timer = updateInterval;
        Evaluate();
    }

    void Evaluate()
    {
        Camera cam = viewer != null ? viewer : Camera.main;
        if (cam == null)
            return;

        TerrainGrid grid = _terrain.Grid;
        Vector3 eye = cam.transform.position;
        float half = grid.ChunkWorldSize * 0.5f;

        // Pass 1: desired render Lod per chunk from distance, damped by hysteresis vs its current render Lod.
        _target.Clear();
        foreach (KeyValuePair<Vector3Int, TerrainChunk> kv in _terrain.Chunks)
        {
            TerrainChunk chunk = kv.Value;
            Vector3 center = chunk.transform.TransformPoint(new Vector3(half, half, half));
            float dist = Vector3.Distance(eye, center);
            _target[kv.Key] = DesiredLod(dist, chunk.RenderLod, grid.MaxLod);
        }

        // Pass 2: relax so no two face-neighbours differ by more than one level.
        if (balanceNeighbors)
            BalanceTargets();

        // Pass 3: apply (SetChunkRenderLod clamps to each chunk's storage Lod and rebuilds only on change).
        foreach (KeyValuePair<Vector3Int, TerrainChunk> kv in _terrain.Chunks)
            if (_target.TryGetValue(kv.Key, out int lod))
                _terrain.SetChunkRenderLod(kv.Value, lod);
    }

    /// <summary>Distance → render Lod, but refusing to change unless the distance is a full <see cref="hysteresis"/> past the band boundary.</summary>
    int DesiredLod(float distance, int currentRenderLod, int maxLod)
    {
        int raw = LodAt(distance, maxLod);
        if (raw > currentRenderLod && LodAt(distance - hysteresis, maxLod) <= currentRenderLod)
            return currentRenderLod; // not yet far enough past the boundary to coarsen
        if (raw < currentRenderLod && LodAt(distance + hysteresis, maxLod) >= currentRenderLod)
            return currentRenderLod; // not yet close enough past the boundary to refine
        return raw;
    }

    int LodAt(float distance, int maxLod)
    {
        int lod = 0;
        if (lodDistances != null)
            for (int i = 0; i < lodDistances.Length; i++)
                if (distance >= lodDistances[i])
                    lod = i + 1;

        // Clamp to the configured LOD count and to what the chunk's stored data can downsample to.
        return Mathf.Clamp(lod, 0, Mathf.Min(lodCount - 1, maxLod));
    }

    /// <summary>Lower any target that sits more than one level above a face-neighbour, until the whole field is within-one.</summary>
    void BalanceTargets()
    {
        bool changed = true;
        int guard = 0;
        while (changed && guard++ < 64)
        {
            changed = false;
            foreach (KeyValuePair<Vector3Int, TerrainChunk> kv in _terrain.Chunks)
            {
                Vector3Int coord = kv.Key;
                int lod = _target[coord];
                foreach (Vector3Int offset in FaceOffsets)
                {
                    if (_target.TryGetValue(coord + offset, out int neighborLod) && lod > neighborLod + 1)
                    {
                        lod = neighborLod + 1;
                        _target[coord] = lod;
                        changed = true;
                    }
                }
            }
        }
    }
}
