using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// PART 1 — Terraforming Tool (chunk manager).
///
/// Owns a dictionary of <see cref="TerrainChunk"/>s keyed by chunk coordinate and
/// exposes the density-editing API (<see cref="Sculpt"/>/<see cref="Paint"/>) plus
/// world seeding (<see cref="GenerateOrLoad"/>). It is EDITOR-DRIVEN: it has no
/// runtime input of its own, and carries no serialized value configuration — those
/// settings live in the editor's TerrainSculptSettings and are pushed into the
/// component's non-serialized fields before use (only asset references are serialized
/// here). When an edit reaches beyond the built area, the touched chunk coordinates
/// don't exist yet, so a fresh chunk (a new density matrix on a new GameObject) is
/// created on demand — this is what lets you "build past the edge".
///
/// Density convention: a sample is SOLID below <see cref="isoLevel"/> and AIR
/// above it. New/empty chunks start as pure air, whose value is derived from the
/// iso level (<see cref="TerrainGrid.AirDensity"/>) so no iso-level setting can
/// make empty space read as solid.
///
/// Configuration lives in the fields below, but the lattice MATH derived from
/// them (base ⇄ Lod ⇄ world conversions, ownership, resolution picking) is owned
/// by a single immutable <see cref="TerrainGrid"/> value — see <see cref="Grid"/>.
/// Those fields are kept valid at all times by <see cref="ValidateConfig"/>
/// (called from <see cref="OnValidate"/>, <see cref="Awake"/> and after a load),
/// so no inspector value can put the grid into a state that divides by zero,
/// allocates a zero-cell chunk, or misaligns strides.
///
/// Resolution: <see cref="chunkSize"/>/<see cref="cellSize"/> are the BASE (finest,
/// Lod 0) grid values. Every chunk keeps the same world-space footprint
/// (chunkSize * cellSize) regardless of its own <see cref="TerrainChunk.Lod"/> —
/// a coarser chunk just stores fewer samples across that same footprint. This is
/// what lets some regions carry more sculpted detail than others while still
/// saving memory/file size in the untouched, coarse regions.
///
/// Seams: within a shared LOD, chunk C and chunk C+1 share one row of samples
/// (C's index N == C+1's index 0, same world point) and we keep those equal by
/// (a) writing every sculpt edit to *all* chunks that store the affected world
/// sample, and (b) copying shared border samples from existing neighbours when a
/// chunk is born or refined. Face-adjacent chunks are only ever allowed to differ
/// by one Lod level (<see cref="BalanceNeighbors"/>); reconciling the *visual*
/// seam where they differ is a mesh-generation concern (transition cells), not
/// yet implemented here (Phase A of the terrain-resolution rework — regular
/// marching cubes only, differing-LOD boundaries will visibly crack until Phase
/// B adds transition cells).
/// </summary>
public class TerrainTool : MonoBehaviour
{
    // ---- Configuration -----------------------------------------------------
    // Value configuration lives in the editor tool (TerrainSculptSettings) and is PUSHED
    // into these NON-serialized runtime fields before use (see TerrainSculptSettings.PushTo).
    // Only the asset references further down stay serialized on the component, so the
    // terrain is editor-driven — configure/sculpt it via the Terrain Tool window.

    [System.NonSerialized] public int chunkSize = 8;
    [System.NonSerialized] public float cellSize = 0.5f;
    [System.NonSerialized] public int maxLod = 3;
    [System.NonSerialized] public int defaultLod = 3;
    [System.NonSerialized] public float isoLevel = 0f;
    [System.NonSerialized] public bool smoothShading = false;

    [System.NonSerialized] public Vector3Int initialChunks = new Vector3Int(8, 4, 8);
    [System.NonSerialized] public float slabThickness = 8f;
    [System.NonSerialized] public float slabMargin = 2f;

    [Header("Assets (the only serialized state on this component)")]
    [Tooltip("Material applied to every chunk renderer. Use the Onepen/TerrainSplat shader for texture painting.")]
    public Material chunkMaterial;
    [Tooltip("Textures paintable onto the terrain; list index = texture slice. All must share size/format. " +
             "Baked into a Texture2DArray if one isn't assigned below.")]
    public Texture2D[] paintTextures;
    [Tooltip("Per-texture world-space tiling multiplier (one Vector2 per Paint Textures slot). " +
             "Scales how often that texture repeats on top of the material's Texture Scale; (1,1) = default.")]
    public Vector2[] paintTextureTiling;
    [Tooltip("Optional prebuilt texture array. If set, it's bound to the chunk material directly and Paint Textures is ignored.")]
    public Texture2DArray textureArray;
    [Tooltip("If set, load this saved terrain instead of building the slab. Density (voxel) data, so it stays fully sculptable.")]
    public TextAsset startupData;
    [Tooltip("At runtime, automatically load Startup Data (or generate the slab) and split it into chunk GameObjects on Awake. " +
             "In the editor the terrain stays a single object; the split only happens in Play. Turn off to drive generation yourself.")]
    public bool buildAtRuntime = true;

    /// <summary>Build raises/adds solid and Carve lowers/removes it (both reshape the density field); Paint blends a texture onto the surface (material) without changing its shape; Smooth relaxes the density field (rounds off bumps/jaggies) without adding bulk.</summary>
    public enum BrushMode { Build, Carve, Smooth, Paint }

    /// <summary>Upper bound on base chunk size, so a single chunk can't ask for an absurd amount of memory.</summary>
    const int MaxChunkSize = 128;

    /// <summary>
    /// Fired with a chunk coordinate immediately BEFORE that chunk is created or
    /// any of its stored data (density/material/Lod) is mutated. Lets the editor
    /// sculpting tool snapshot the exact pre-edit state of every affected chunk —
    /// including LOD-balanced neighbours — so a whole stroke can be undone.
    /// </summary>
    public event System.Action<Vector3Int> BeforeChunkEdit;

    void FireBeforeEdit(Vector3Int coord) => BeforeChunkEdit?.Invoke(coord);

    // The one source of truth for all lattice math, rebuilt whenever config changes.
    TerrainGrid _grid;
    /// <summary>Immutable grid config + conversions, always in sync with the (validated) inspector fields.</summary>
    public TerrainGrid Grid => _grid;

    readonly Dictionary<Vector3Int, TerrainChunk> _chunks = new Dictionary<Vector3Int, TerrainChunk>();
    readonly HashSet<Vector3Int> _dirty = new HashSet<Vector3Int>();

    // Reused scratch so per-sample edits during a drag don't allocate.
    readonly List<KeyValuePair<Vector3Int, Vector3Int>> _owners =
        new List<KeyValuePair<Vector3Int, Vector3Int>>(8);
    // A second buffer the edit loops iterate, so a reentrant SampleOwners (via GetOrCreateChunk →
    // InitBorders → TryGetExistingSample) can refill _owners without invalidating the enumeration.
    readonly List<KeyValuePair<Vector3Int, Vector3Int>> _ownersSnapshot =
        new List<KeyValuePair<Vector3Int, Vector3Int>>(8);

    static readonly Vector3Int[] FaceOffsets =
    {
        new Vector3Int(-1, 0, 0), new Vector3Int(1, 0, 0),
        new Vector3Int(0, -1, 0), new Vector3Int(0, 1, 0),
        new Vector3Int(0, 0, -1), new Vector3Int(0, 0, 1),
    };

    // Editor: the terrain is edited as a single hidden-chunk object (see GetOrCreateChunk).
    // Runtime (Awake only runs in Play): load the saved data — or generate the slab — and
    // split it into chunk GameObjects, so culling/LOD have real per-chunk renderers to work
    // with. Set buildAtRuntime = false to drive generation yourself instead.
    void Awake()
    {
        if (buildAtRuntime)
        {
            GenerateOrLoad(); // loads startupData (or the slab) and builds the chunk set
        }
        else
        {
            ValidateConfig();
            BuildTextureArray();
        }
    }

    /// <summary>
    /// Bind the paint textures to the chunk material's <c>_TexArray</c>: the
    /// prebuilt <see cref="textureArray"/> asset if one is assigned, otherwise a
    /// <see cref="Texture2DArray"/> assembled at startup from <see cref="paintTextures"/>.
    /// </summary>
    /// <summary>
    /// Guarantee a <see cref="chunkMaterial"/> exists before any chunk is created, so the
    /// terrain always renders with the splat shader even when none was assigned in the
    /// inspector. Falls back to a material built from the <c>Onepen/TerrainSplat</c> shader.
    /// The editor upgrades this to a saved .mat asset (see TerrainToolActions.EnsureMaterialAsset).
    /// </summary>
    public void EnsureMaterial()
    {
        if (chunkMaterial != null)
            return;

        Shader shader = Shader.Find("Onepen/TerrainSplat");
        if (shader == null)
        {
            Debug.LogWarning("[TerrainTool] 'Onepen/TerrainSplat' shader not found; chunks will have no material.");
            return;
        }
        chunkMaterial = new Material(shader) { name = "TerrainSplat (auto)" };
    }

    void BuildTextureArray()
    {
        EnsureMaterial();
        if (chunkMaterial == null)
            return;

        Texture2DArray array = textureArray != null ? textureArray : CreateTextureArray(paintTextures);
        if (array != null)
            chunkMaterial.SetTexture("_TexArray", array);

        ApplyTextureTiling();
    }

    /// <summary>
    /// Upload the per-texture tiling multipliers (<see cref="paintTextureTiling"/>) to the
    /// chunk material's <c>_TexTiling</c> array, one entry per texture slice. A missing or
    /// non-positive entry defaults to (1,1). Cheap — call it whenever the tiling changes,
    /// without rebuilding the texture array.
    /// </summary>
    public void ApplyTextureTiling()
    {
        if (chunkMaterial == null)
            return;

        var tiling = new Vector4[MatSplat.SlotCount];
        for (int i = 0; i < tiling.Length; i++)
        {
            Vector2 t = paintTextureTiling != null && i < paintTextureTiling.Length ? paintTextureTiling[i] : Vector2.one;
            tiling[i] = new Vector4(t.x > 0f ? t.x : 1f, t.y > 0f ? t.y : 1f, 0f, 0f);
        }
        chunkMaterial.SetVectorArray("_TexTiling", tiling);
    }

    /// <summary>Working resolution for the assembled texture array (each source is rescaled to this).</summary>
    const int TextureArraySize = 512;

    /// <summary>
    /// Assemble a set of textures into a <see cref="Texture2DArray"/> whose slice
    /// index matches each texture's position in the list. Every source is blitted
    /// (rescaled and reformatted) into a fixed-size RGBA slice, so textures of any
    /// size/format/compression are handled and no slice is ever left blank. Returns
    /// null if there's nothing to build from. Shared by the runtime startup path and
    /// the editor "bake to asset" button.
    /// </summary>
    public static Texture2DArray CreateTextureArray(Texture2D[] textures)
    {
        if (textures == null || textures.Length == 0)
            return null;

        bool any = false;
        foreach (Texture2D t in textures)
        {
            if (t != null) { any = true; break; }
        }
        if (!any)
            return null;

        int size = TextureArraySize;
        var array = new Texture2DArray(size, size, textures.Length, TextureFormat.RGBA32, mipChain: true, linear: false)
        {
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear,
        };

        var rt = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var temp = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false, linear: false);
        RenderTexture prev = RenderTexture.active;

        for (int i = 0; i < textures.Length; i++)
        {
            Texture2D src = textures[i];
            if (src == null)
            {
                Debug.LogWarning($"[TerrainTool] paintTextures[{i}] is empty; slice {i} left blank.");
                continue;
            }

            Graphics.Blit(src, rt); // rescales/reformats any source into the slice resolution
            RenderTexture.active = rt;
            temp.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            temp.Apply();
            array.SetPixels(temp.GetPixels(), i, 0);
        }

        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        if (Application.isPlaying)
            Destroy(temp);
        else
            DestroyImmediate(temp);

        array.Apply(updateMipmaps: true);
        return array;
    }

    /// <summary>
    /// Clamp every configuration field into a range that keeps the grid coherent
    /// (power-of-two chunk size, a maxLod the chunk size can actually support,
    /// a positive cell size, a default Lod within range) and rebuild <see cref="Grid"/>.
    /// This is the single place bad inspector/save values are made safe, so no
    /// other code has to defend against them.
    /// </summary>
    public void ValidateConfig()
    {
        chunkSize = Mathf.Clamp(Mathf.ClosestPowerOfTwo(Mathf.Max(chunkSize, 2)), 2, MaxChunkSize);
        cellSize = Mathf.Max(cellSize, TerrainGrid.MinCellSize);
        maxLod = Mathf.Clamp(maxLod, 0, TerrainGrid.MaxLodFor(chunkSize));
        defaultLod = Mathf.Clamp(defaultLod, 0, maxLod);

        _grid = new TerrainGrid(chunkSize, cellSize, maxLod, isoLevel);
    }

    // ---- Resolution ----------------------------------------------------------

    /// <summary>Nearest valid Lod (clamped to [0, maxLod]) for a desired world-space cell size.</summary>
    public int LodForCellSize(float desiredCellSize) => _grid.LodForCellSize(desiredCellSize);

    /// <summary>Refine a chunk to a finer Lod (resample + prefer exact data from any already-finer-or-equal neighbour over the interpolated guess). No-op if already at least that fine.</summary>
    public void RefineChunk(TerrainChunk chunk, int newLod)
    {
        newLod = Mathf.Clamp(newLod, 0, maxLod);
        if (newLod >= chunk.Lod)
            return;

        FireBeforeEdit(chunk.Coord);

        int newChunkSize = _grid.CellsAtLod(newLod);
        float[,,] resampled = LodResample.Refine(chunk.Density, chunk.ChunkSize, newChunkSize);
        MatSplat[,,] resampledMat = LodResample.RefineMaterial(chunk.Material, chunk.ChunkSize, newChunkSize);

        int oldStride = _grid.StrideAtLod(chunk.Lod);
        int newStride = _grid.StrideAtLod(newLod);
        int newN = newChunkSize + 1;
        for (int x = 0; x < newN; x++)
        for (int y = 0; y < newN; y++)
        for (int z = 0; z < newN; z++)
        {
            bool onFace = x == 0 || y == 0 || z == 0 || x == newChunkSize || y == newChunkSize || z == newChunkSize;
            if (!onFace)
                continue; // interior new samples have no neighbour to consult — the interpolation is already the best we can do
            if (x % oldStride == 0 && y % oldStride == 0 && z % oldStride == 0)
                continue; // an exact old sample, not a newly-introduced one — trust it as-is

            Vector3Int ws = _grid.SampleWorld(chunk.Coord, x * newStride, y * newStride, z * newStride);
            if (TryGetExistingSample(ws, chunk.Coord, newLod, out float exact))
                resampled[x, y, z] = exact;
            if (TryGetExistingMaterialSample(ws, chunk.Coord, newLod, out MatSplat exactMat))
                resampledMat[x, y, z] = exactMat;
        }

        chunk.ApplyResampled(resampled, resampledMat, newLod, newChunkSize, _grid.SpacingAtLod(newLod));
        _dirty.Add(chunk.Coord);
        MarkFaceNeighborsDirty(chunk.Coord);
    }

    /// <summary>Coarsen a chunk to a cheaper Lod (exact point-pick — no interpolation needed since the ratio is always a power of two). No-op if already at least that coarse.</summary>
    public void CoarsenChunk(TerrainChunk chunk, int newLod)
    {
        newLod = Mathf.Clamp(newLod, 0, maxLod);
        if (newLod <= chunk.Lod)
            return;

        FireBeforeEdit(chunk.Coord);

        int newChunkSize = _grid.CellsAtLod(newLod);
        float[,,] resampled = LodResample.Coarsen(chunk.Density, chunk.ChunkSize, newChunkSize);
        MatSplat[,,] resampledMat = LodResample.CoarsenMaterial(chunk.Material, chunk.ChunkSize, newChunkSize);

        chunk.ApplyResampled(resampled, resampledMat, newLod, newChunkSize, _grid.SpacingAtLod(newLod));
        _dirty.Add(chunk.Coord);
        MarkFaceNeighborsDirty(chunk.Coord);
    }

    /// <summary>Ensure a chunk exists and is at least as fine as targetLod, then cascade-refine any face-neighbour that would otherwise end up more than one Lod level coarser.</summary>
    void EnsureLod(Vector3Int coord, int targetLod)
    {
        TerrainChunk chunk = GetOrCreateChunk(coord, targetLod);
        if (chunk.Lod > targetLod)
            RefineChunk(chunk, targetLod);

        BalanceNeighbors(coord);
    }

    /// <summary>Cascade-refine face-neighbours of (chunks reachable from) startCoord so no two face-adjacent chunks ever differ by more than one Lod level.</summary>
    void BalanceNeighbors(Vector3Int startCoord)
    {
        var queue = new Queue<Vector3Int>();
        queue.Enqueue(startCoord);

        while (queue.Count > 0)
        {
            Vector3Int coord = queue.Dequeue();
            if (!_chunks.TryGetValue(coord, out TerrainChunk chunk))
                continue;

            foreach (Vector3Int offset in FaceOffsets)
            {
                Vector3Int neighborCoord = coord + offset;
                if (!_chunks.TryGetValue(neighborCoord, out TerrainChunk neighbor))
                    continue;

                if (neighbor.Lod > chunk.Lod + 1)
                {
                    RefineChunk(neighbor, chunk.Lod + 1);
                    queue.Enqueue(neighborCoord);
                }
            }
        }
    }

    void MarkFaceNeighborsDirty(Vector3Int coord)
    {
        foreach (Vector3Int offset in FaceOffsets)
        {
            Vector3Int neighbor = coord + offset;
            if (_chunks.ContainsKey(neighbor))
                _dirty.Add(neighbor);
        }
    }

    // ---- Sculpting ---------------------------------------------------------

    /// <summary>
    /// Apply a spherical-falloff density delta centred at a world point, and
    /// rebuild every chunk it touched. Every chunk the brush cube can reach is
    /// first refined to at least <paramref name="targetCellSize"/> (never
    /// auto-coarsened), keeping face-neighbours within one Lod level, before the
    /// density edit itself is applied. A non-positive radius is a no-op (nothing
    /// to sculpt, and it would make the falloff undefined at the centre).
    /// </summary>
    public void Sculpt(Vector3 worldPoint, float amount, float radius, float targetCellSize)
    {
        if (radius <= 0f)
            return;

        // amount = sign * strength * dabStep: negative builds (adds solid), positive carves.
        bool remove = amount > 0f;
        float rate = Mathf.Clamp01(Mathf.Abs(amount)); // how far this dab moves toward the full sphere stamp
        if (rate <= 0f)
            return;

        int targetLod = _grid.LodForCellSize(targetCellSize);

        // Manager-local position, expressed in world-sample units.
        Vector3 center = transform.InverseTransformPoint(worldPoint) / _grid.CellSize;
        float r = radius / _grid.CellSize;
        float reach = r + 2f; // samples the stamp (incl. its surface band) can affect
        int ri = Mathf.CeilToInt(reach);

        int cx = Mathf.RoundToInt(center.x);
        int cy = Mathf.RoundToInt(center.y);
        int cz = Mathf.RoundToInt(center.z);

        // Pass 1: make sure every chunk the brush can reach can represent targetCellSize.
        var touchedChunks = new HashSet<Vector3Int>();
        for (int x = cx - ri; x <= cx + ri; x++)
        for (int y = cy - ri; y <= cy + ri; y++)
        for (int z = cz - ri; z <= cz + ri; z++)
        {
            if (Vector3.Distance(new Vector3(x, y, z), center) > reach)
                continue;

            touchedChunks.Add(new Vector3Int(
                Mathf.FloorToInt((float)x / chunkSize),
                Mathf.FloorToInt((float)y / chunkSize),
                Mathf.FloorToInt((float)z / chunkSize)));
        }
        foreach (Vector3Int coord in touchedChunks)
            EnsureLod(coord, targetLod);

        // Pass 2: CSG-union (build) or subtract (carve) a sphere's signed distance into the field.
        // Because the target is a connected signed-distance sphere, material stamped onto existing
        // terrain is ALWAYS joined to it (no pinched-off blobs) and the surface stays smooth.
        for (int x = cx - ri; x <= cx + ri; x++)
        for (int y = cy - ri; y <= cy + ri; y++)
        for (int z = cz - ri; z <= cz + ri; z++)
        {
            float dist = Vector3.Distance(new Vector3(x, y, z), center);
            if (dist > reach)
                continue;

            float sphereSdf = (dist - r) * _grid.CellSize; // world-unit distance to the sphere surface (<0 inside)
            StampWorldSample(new Vector3Int(x, y, z), sphereSdf, rate, remove);
        }

        RebuildDirty();
    }

    /// <summary>
    /// Relax the surface under the brush by pulling every density sample toward the
    /// local average of its 3×3×3 neighbourhood — a box blur of the signed field,
    /// which rounds off sharp ridges and fills jagged pits WITHOUT adding or removing
    /// bulk material. Like <see cref="Sculpt"/> it first refines every reachable chunk —
    /// expanded by a one-sample skirt so the blur's neighbour taps around the rim are
    /// full resolution too — to <paramref name="targetCellSize"/> (never auto-coarsened).
    /// <paramref name="strength"/> is the per-dab fraction (0..1) each sample moves toward
    /// its neighbourhood average, scaled by a radial falloff so the brush feathers at its
    /// rim. A non-positive radius or strength is a no-op.
    /// </summary>
    public void Smooth(Vector3 worldPoint, float strength, float radius, float targetCellSize)
    {
        if (radius <= 0f)
            return;
        float rate = Mathf.Clamp01(strength);
        if (rate <= 0f)
            return;

        int targetLod = _grid.LodForCellSize(targetCellSize);

        Vector3 center = transform.InverseTransformPoint(worldPoint) / _grid.CellSize;
        float r = radius / _grid.CellSize;
        int ri = Mathf.CeilToInt(r);

        int cx = Mathf.RoundToInt(center.x);
        int cy = Mathf.RoundToInt(center.y);
        int cz = Mathf.RoundToInt(center.z);

        // Pass 1: refine every chunk the brush can reach — expanded by one sample so the
        // blur's neighbour taps around the rim are stored at full resolution too.
        var touchedChunks = new HashSet<Vector3Int>();
        for (int x = cx - ri - 1; x <= cx + ri + 1; x++)
        for (int y = cy - ri - 1; y <= cy + ri + 1; y++)
        for (int z = cz - ri - 1; z <= cz + ri + 1; z++)
        {
            if (Vector3.Distance(new Vector3(x, y, z), center) > r + 1f)
                continue;

            touchedChunks.Add(new Vector3Int(
                Mathf.FloorToInt((float)x / chunkSize),
                Mathf.FloorToInt((float)y / chunkSize),
                Mathf.FloorToInt((float)z / chunkSize)));
        }
        foreach (Vector3Int coord in touchedChunks)
            EnsureLod(coord, targetLod);

        // Pass 2: compute every blurred target from the PRE-smooth field first, so the blur
        // reads original neighbour values (not ones already half-smoothed this dab), THEN apply.
        var blurred = new Dictionary<Vector3Int, float>();
        for (int x = cx - ri; x <= cx + ri; x++)
        for (int y = cy - ri; y <= cy + ri; y++)
        for (int z = cz - ri; z <= cz + ri; z++)
        {
            float dist = Vector3.Distance(new Vector3(x, y, z), center);
            if (dist > r)
                continue;

            float sum = 0f;
            for (int ox = -1; ox <= 1; ox++)
            for (int oy = -1; oy <= 1; oy++)
            for (int oz = -1; oz <= 1; oz++)
                sum += SampleDensityOrAir(new Vector3Int(x + ox, y + oy, z + oz));
            float avg = sum / 27f; // uniform 3×3×3 box blur of the signed field

            var ws = new Vector3Int(x, y, z);
            float falloff = 1f - dist / r;                       // feather toward the rim
            float current = SampleDensityOrAir(ws);
            blurred[ws] = Mathf.Lerp(current, avg, rate * falloff);
        }

        foreach (KeyValuePair<Vector3Int, float> kv in blurred)
            SetWorldSample(kv.Key, kv.Value);

        RebuildDirty();
    }

    /// <summary>
    /// Paint a material onto the terrain without changing its shape. The brush has a
    /// solid core (the material is driven to full weight) and only a tapered rim band
    /// — the outer <paramref name="edgeBlend"/> fraction of the radius — settles at a
    /// partial weight, so the painted zone is uniformly textured inside and only its
    /// edge cross-fades into the surrounding textures. <paramref name="rate"/>
    /// (strength * deltaTime) controls how quickly it builds toward that profile. Like
    /// <see cref="Sculpt"/> it first refines every reachable chunk to
    /// <paramref name="targetCellSize"/> so fine paint detail has samples to live on.
    /// A non-positive radius or rate is a no-op.
    /// </summary>
    public void Paint(Vector3 worldPoint, int material, float rate, float radius, float edgeBlend, float targetCellSize)
    {
        if (radius <= 0f || rate <= 0f)
            return;

        int targetLod = _grid.LodForCellSize(targetCellSize);

        Vector3 center = transform.InverseTransformPoint(worldPoint) / _grid.CellSize;
        float r = radius / _grid.CellSize;
        int ri = Mathf.CeilToInt(r);

        int cx = Mathf.RoundToInt(center.x);
        int cy = Mathf.RoundToInt(center.y);
        int cz = Mathf.RoundToInt(center.z);

        // Pass 1: make sure every chunk the brush cube can reach can represent targetCellSize.
        var touchedChunks = new HashSet<Vector3Int>();
        for (int x = cx - ri; x <= cx + ri; x++)
        for (int y = cy - ri; y <= cy + ri; y++)
        for (int z = cz - ri; z <= cz + ri; z++)
        {
            if (Vector3.Distance(new Vector3(x, y, z), center) > r)
                continue;

            touchedChunks.Add(new Vector3Int(
                Mathf.FloorToInt((float)x / chunkSize),
                Mathf.FloorToInt((float)y / chunkSize),
                Mathf.FloorToInt((float)z / chunkSize)));
        }
        foreach (Vector3Int coord in touchedChunks)
            EnsureLod(coord, targetLod);

        // Pass 2: drive each sample toward the brush profile — full in the core,
        // tapering across the outer edgeBlend band so only the rim cross-fades.
        float inner = r * (1f - Mathf.Clamp01(edgeBlend)); // radius where the taper starts
        for (int x = cx - ri; x <= cx + ri; x++)
        for (int y = cy - ri; y <= cy + ri; y++)
        for (int z = cz - ri; z <= cz + ri; z++)
        {
            float dist = Vector3.Distance(new Vector3(x, y, z), center);
            if (dist > r)
                continue;

            float target = dist <= inner ? 1f : (r - dist) / Mathf.Max(r - inner, 1e-5f); // 1 in core -> 0 at rim
            PaintWorldSample(new Vector3Int(x, y, z), material, target, rate);
        }

        RebuildDirty();
    }

    /// <summary>
    /// CSG-stamp a brush sphere into a world sample, writing the result to every chunk that stores
    /// it (1, 2, 4 or 8 along shared borders), creating chunks as needed. Build unions the sphere
    /// (density moves toward the more-solid of the two SDFs); carve subtracts it. Because the stamp
    /// target is a connected signed-distance sphere, built material always joins whatever it
    /// overlaps — so sculpting can't leave pinched-off, disconnected blobs.
    /// </summary>
    void StampWorldSample(Vector3Int ws, float sphereSdf, float rate, bool remove)
    {
        SampleOwners(ws);
        _ownersSnapshot.Clear();
        _ownersSnapshot.AddRange(_owners); // iterate a copy: GetOrCreateChunk below can refill _owners reentrantly

        // Compute the ONE new value for this world sample up front, then write it to every owner.
        // Build unions a solid sphere (density → the nearer-to-solid of the two SDFs); carve subtracts
        // it. rate controls how far this dab moves toward that target. Reading once and SETTING every
        // copy keeps shared border samples identical regardless of chunk-creation order.
        float current = CurrentSharedDensity(ws);
        float target = remove ? Mathf.Max(current, -sphereSdf) : Mathf.Min(current, sphereSdf);
        float value = Mathf.Lerp(current, target, rate);

        // No-op if nothing actually changed (samples outside the stamp's reach).
        if (value == current)
            return;

        foreach (var owner in _ownersSnapshot)
        {
            TerrainChunk chunk = GetOrCreateChunk(owner.Key);
            if (!_grid.TryLocalIndex(chunk.Lod, owner.Value, out Vector3Int local))
                continue;

            FireBeforeEdit(owner.Key);
            chunk.Density[local.x, local.y, local.z] = value;

            // A newly-solid, never-painted sample gets the base material so painted
            // edges blend into sculpted ground instead of meeting it with a hard seam.
            if (value < _grid.IsoLevel && chunk.Material[local.x, local.y, local.z].IsEmpty)
                chunk.Material[local.x, local.y, local.z] = MatSplat.Single(0);

            _dirty.Add(owner.Key);
        }
    }

    /// <summary>The shared density of a world sample, read from any existing owner that stores it (air if none do yet).</summary>
    float CurrentSharedDensity(Vector3Int ws)
    {
        foreach (var owner in _ownersSnapshot)
            if (_chunks.TryGetValue(owner.Key, out TerrainChunk c) &&
                _grid.TryLocalIndex(c.Lod, owner.Value, out Vector3Int li))
                return c.Density[li.x, li.y, li.z];
        return _grid.AirDensity;
    }

    /// <summary>
    /// Overwrite a world sample's density with an explicit value in every chunk that
    /// stores it (1, 2, 4 or 8 along shared borders), mirroring <see cref="StampWorldSample"/>'s
    /// multi-owner write so shared border samples stay identical. Used by <see cref="Smooth"/>
    /// and <see cref="CleanUp"/>, which compute the new value themselves rather than CSG-stamping
    /// a shape. A sample pushed below the iso level with no material yet gets the base layer, so
    /// newly-solid ground renders textured instead of blank.
    /// </summary>
    void SetWorldSample(Vector3Int ws, float value)
    {
        SampleOwners(ws);
        _ownersSnapshot.Clear();
        _ownersSnapshot.AddRange(_owners); // iterate a copy: GetOrCreateChunk below can refill _owners reentrantly

        foreach (var owner in _ownersSnapshot)
        {
            TerrainChunk chunk = GetOrCreateChunk(owner.Key);
            if (!_grid.TryLocalIndex(chunk.Lod, owner.Value, out Vector3Int local))
                continue;

            FireBeforeEdit(owner.Key);
            chunk.Density[local.x, local.y, local.z] = value;
            if (value < _grid.IsoLevel && chunk.Material[local.x, local.y, local.z].IsEmpty)
                chunk.Material[local.x, local.y, local.z] = MatSplat.Single(0);
            _dirty.Add(owner.Key);
        }
    }

    /// <summary>The density of a world sample from any existing owner that stores it, or air if none do (used by the smooth brush's neighbourhood reads). Standalone — it runs its own owner lookup rather than relying on a prior <see cref="SampleOwners"/>.</summary>
    float SampleDensityOrAir(Vector3Int ws)
    {
        SampleOwners(ws);
        foreach (var owner in _owners)
            if (_chunks.TryGetValue(owner.Key, out TerrainChunk c) &&
                _grid.TryLocalIndex(c.Lod, owner.Value, out Vector3Int li))
                return c.Density[li.x, li.y, li.z];
        return _grid.AirDensity;
    }

    // ---- Automatic cleanup -------------------------------------------------

    /// <summary>Guard: refuse to scan an absurdly large bounding box (keeps a one-click cleanup from allocating gigabytes / hanging).</summary>
    const long MaxCleanupSamples = 64_000_000L;

    /// <summary>
    /// One-click cleanup of sculpting junk: delete small DISCONNECTED solid blobs
    /// (<paramref name="removeDebris"/>) and fill small FULLY-ENCLOSED air pockets
    /// (<paramref name="fillHoles"/>). A connected-components pass over the base
    /// world-sample lattice: any solid cluster of at most <paramref name="maxClusterVoxels"/>
    /// samples that isn't joined to the bulk terrain is cleared to air, and any enclosed
    /// air cluster of at most that size (one not reachable from outside the terrain) is
    /// filled solid. Larger structures — the terrain itself, intentional caverns — are
    /// untouched because they exceed the threshold. Editor chunks are stored at full
    /// resolution (Lod 0), so every integer sample in the terrain's footprint is present
    /// and the lattice reads consistently. Returns the number of samples changed.
    /// </summary>
    public int CleanUp(int maxClusterVoxels, bool removeDebris, bool fillHoles)
    {
        if (_chunks.Count == 0 || maxClusterVoxels <= 0 || (!removeDebris && !fillHoles))
            return 0;

        // Bounding box of every stored sample, in base/Lod-0 world-sample coordinates.
        var minC = new Vector3Int(int.MaxValue, int.MaxValue, int.MaxValue);
        var maxC = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
        foreach (Vector3Int c in _chunks.Keys)
        {
            minC = Vector3Int.Min(minC, c);
            maxC = Vector3Int.Max(maxC, c);
        }
        Vector3Int lo = minC * chunkSize;
        Vector3Int hi = (maxC + Vector3Int.one) * chunkSize; // chunk's samples reach one chunkSize past its origin
        int nx = hi.x - lo.x + 1, ny = hi.y - lo.y + 1, nz = hi.z - lo.z + 1;

        if ((long)nx * ny * nz > MaxCleanupSamples)
        {
            Debug.LogWarning($"[TerrainTool] Clean Up skipped: terrain bounds ({nx}×{ny}×{nz}) are too large to scan.");
            return 0;
        }

        // Classify every lattice sample as solid (density < iso) or air. Missing-chunk regions read as air.
        int n = nx * ny * nz;
        var solid = new bool[n];
        for (int x = 0; x < nx; x++)
        for (int y = 0; y < ny; y++)
        for (int z = 0; z < nz; z++)
            solid[Flatten(x, y, z, ny, nz)] =
                SampleDensityOrAir(new Vector3Int(lo.x + x, lo.y + y, lo.z + z)) < _grid.IsoLevel;

        var toAir = new List<int>();   // solid debris to clear
        var toSolid = new List<int>(); // enclosed holes to fill

        if (removeDebris)
            CollectSmallComponents(solid, target: true, nx, ny, nz, maxClusterVoxels, toAir);
        foreach (int i in toAir)
            solid[i] = false; // apply to the working field so hole detection sees the post-debris state

        if (fillHoles)
            CollectSmallEnclosedAir(solid, nx, ny, nz, maxClusterVoxels, toSolid);

        // Write results back through the shared-sample writer so seams stay consistent.
        foreach (int i in toAir)
            SetWorldSample(WorldFromFlat(i, lo, ny, nz), _grid.AirDensity);

        float fill = _grid.IsoLevel - _grid.CellSize; // firmly inside, so the pocket's inner surface disappears
        foreach (int i in toSolid)
            SetWorldSample(WorldFromFlat(i, lo, ny, nz), fill);

        RebuildDirty();
        return toAir.Count + toSolid.Count;
    }

    static int Flatten(int x, int y, int z, int ny, int nz) => (x * ny + y) * nz + z;

    static Vector3Int WorldFromFlat(int i, Vector3Int lo, int ny, int nz)
    {
        int z = i % nz;
        int y = (i / nz) % ny;
        int x = i / (nz * ny);
        return new Vector3Int(lo.x + x, lo.y + y, lo.z + z);
    }

    /// <summary>
    /// 6-connected flood fill over <paramref name="field"/>: every connected component of cells
    /// equal to <paramref name="target"/> whose size is at most <paramref name="maxSize"/> has its
    /// indices appended to <paramref name="small"/>. The bulk terrain (one huge component) exceeds
    /// the threshold and is skipped, so only stray blobs are collected.
    /// </summary>
    static void CollectSmallComponents(bool[] field, bool target, int nx, int ny, int nz, int maxSize, List<int> small)
    {
        var visited = new bool[field.Length];
        var stack = new Stack<int>();
        var comp = new List<int>();

        for (int i = 0; i < field.Length; i++)
        {
            if (visited[i] || field[i] != target)
                continue;

            comp.Clear();
            stack.Push(i);
            visited[i] = true;
            while (stack.Count > 0)
            {
                int cur = stack.Pop();
                comp.Add(cur);
                int z = cur % nz, y = (cur / nz) % ny, x = cur / (nz * ny);
                PushNeighbors(field, visited, stack, target, x, y, z, nx, ny, nz);
            }

            if (comp.Count <= maxSize)
                small.AddRange(comp);
        }
    }

    /// <summary>
    /// Find small enclosed air pockets in <paramref name="solid"/>: flood "open" air inward from the
    /// bounding-box faces, then any air component not reached (fully walled in by solid) whose size is
    /// at most <paramref name="maxSize"/> is appended to <paramref name="fill"/>. Large enclosed
    /// caverns exceed the threshold and are preserved.
    /// </summary>
    static void CollectSmallEnclosedAir(bool[] solid, int nx, int ny, int nz, int maxSize, List<int> fill)
    {
        var open = new bool[solid.Length]; // air connected to the outside
        var stack = new Stack<int>();

        // Seed from every air cell on the six faces of the box.
        for (int x = 0; x < nx; x++)
        for (int y = 0; y < ny; y++)
        for (int z = 0; z < nz; z++)
        {
            if (x != 0 && x != nx - 1 && y != 0 && y != ny - 1 && z != 0 && z != nz - 1)
                continue;
            int i = Flatten(x, y, z, ny, nz);
            if (!solid[i] && !open[i])
            {
                open[i] = true;
                stack.Push(i);
            }
        }
        while (stack.Count > 0)
        {
            int cur = stack.Pop();
            int z = cur % nz, y = (cur / nz) % ny, x = cur / (nz * ny);
            PushOpenAir(solid, open, stack, x, y, z, nx, ny, nz);
        }

        // Enclosed air = not solid and not open. Collect small components of it.
        var visited = new bool[solid.Length];
        var comp = new List<int>();
        for (int i = 0; i < solid.Length; i++)
        {
            if (solid[i] || open[i] || visited[i])
                continue;

            comp.Clear();
            stack.Push(i);
            visited[i] = true;
            while (stack.Count > 0)
            {
                int cur = stack.Pop();
                comp.Add(cur);
                int z = cur % nz, y = (cur / nz) % ny, x = cur / (nz * ny);
                PushEnclosedAir(solid, open, visited, stack, x, y, z, nx, ny, nz);
            }

            if (comp.Count <= maxSize)
                fill.AddRange(comp);
        }
    }

    // ---- 6-neighbour expansion helpers (shared by the flood fills) ----

    static void PushNeighbors(bool[] field, bool[] visited, Stack<int> stack, bool target, int x, int y, int z, int nx, int ny, int nz)
    {
        if (x > 0)      TryVisit(field, visited, stack, target, x - 1, y, z, ny, nz);
        if (x < nx - 1) TryVisit(field, visited, stack, target, x + 1, y, z, ny, nz);
        if (y > 0)      TryVisit(field, visited, stack, target, x, y - 1, z, ny, nz);
        if (y < ny - 1) TryVisit(field, visited, stack, target, x, y + 1, z, ny, nz);
        if (z > 0)      TryVisit(field, visited, stack, target, x, y, z - 1, ny, nz);
        if (z < nz - 1) TryVisit(field, visited, stack, target, x, y, z + 1, ny, nz);
    }

    static void TryVisit(bool[] field, bool[] visited, Stack<int> stack, bool target, int x, int y, int z, int ny, int nz)
    {
        int i = Flatten(x, y, z, ny, nz);
        if (!visited[i] && field[i] == target)
        {
            visited[i] = true;
            stack.Push(i);
        }
    }

    static void PushOpenAir(bool[] solid, bool[] open, Stack<int> stack, int x, int y, int z, int nx, int ny, int nz)
    {
        if (x > 0)      TryOpen(solid, open, stack, x - 1, y, z, ny, nz);
        if (x < nx - 1) TryOpen(solid, open, stack, x + 1, y, z, ny, nz);
        if (y > 0)      TryOpen(solid, open, stack, x, y - 1, z, ny, nz);
        if (y < ny - 1) TryOpen(solid, open, stack, x, y + 1, z, ny, nz);
        if (z > 0)      TryOpen(solid, open, stack, x, y, z - 1, ny, nz);
        if (z < nz - 1) TryOpen(solid, open, stack, x, y, z + 1, ny, nz);
    }

    static void TryOpen(bool[] solid, bool[] open, Stack<int> stack, int x, int y, int z, int ny, int nz)
    {
        int i = Flatten(x, y, z, ny, nz);
        if (!solid[i] && !open[i])
        {
            open[i] = true;
            stack.Push(i);
        }
    }

    static void PushEnclosedAir(bool[] solid, bool[] open, bool[] visited, Stack<int> stack, int x, int y, int z, int nx, int ny, int nz)
    {
        if (x > 0)      TryEnclosed(solid, open, visited, stack, x - 1, y, z, ny, nz);
        if (x < nx - 1) TryEnclosed(solid, open, visited, stack, x + 1, y, z, ny, nz);
        if (y > 0)      TryEnclosed(solid, open, visited, stack, x, y - 1, z, ny, nz);
        if (y < ny - 1) TryEnclosed(solid, open, visited, stack, x, y + 1, z, ny, nz);
        if (z > 0)      TryEnclosed(solid, open, visited, stack, x, y, z - 1, ny, nz);
        if (z < nz - 1) TryEnclosed(solid, open, visited, stack, x, y, z + 1, ny, nz);
    }

    static void TryEnclosed(bool[] solid, bool[] open, bool[] visited, Stack<int> stack, int x, int y, int z, int ny, int nz)
    {
        int i = Flatten(x, y, z, ny, nz);
        if (!solid[i] && !open[i] && !visited[i])
        {
            visited[i] = true;
            stack.Push(i);
        }
    }

    /// <summary>
    /// Blend a material into a world sample in every chunk that stores it, mirroring
    /// <see cref="StampWorldSample"/> so shared border samples stay equal (each copy
    /// gets the same paint delta). Coarser candidate owners that don't store this
    /// exact sample are skipped.
    /// </summary>
    void PaintWorldSample(Vector3Int ws, int material, float target, float rate)
    {
        SampleOwners(ws);
        _ownersSnapshot.Clear();
        _ownersSnapshot.AddRange(_owners); // iterate a copy: GetOrCreateChunk below can refill _owners reentrantly

        // Blend once from the shared current value, then SET it on every owner — same reasoning as
        // StampWorldSample: blending per-owner diverges the shared border splats and seams the paint.
        MatSplat splat = CurrentSharedMaterial(ws);
        // An unpainted sample (notably the AIR sample just above a flat surface, which every
        // marching-cubes vertex on that surface interpolates toward) carries no base layer, so a
        // partial paint would read as PURE painted texture and the rim taper would collapse into a
        // hard edge. Seed the base layer first — matching FillSlab/StampWorldSample's convention for
        // solid samples — so a partial paint blends against slice 0 instead.
        if (splat.IsEmpty)
            splat = MatSplat.Single(0);
        MatSplat.Paint(ref splat, material, target, rate);

        foreach (var owner in _ownersSnapshot)
        {
            TerrainChunk chunk = GetOrCreateChunk(owner.Key);
            if (!_grid.TryLocalIndex(chunk.Lod, owner.Value, out Vector3Int local))
                continue;

            FireBeforeEdit(owner.Key);
            chunk.Material[local.x, local.y, local.z] = splat;
            _dirty.Add(owner.Key);
        }
    }

    /// <summary>The shared material splat of a world sample, read from any existing owner that stores it.</summary>
    MatSplat CurrentSharedMaterial(Vector3Int ws)
    {
        foreach (var owner in _ownersSnapshot)
            if (_chunks.TryGetValue(owner.Key, out TerrainChunk c) &&
                _grid.TryLocalIndex(c.Lod, owner.Value, out Vector3Int li))
                return c.Material[li.x, li.y, li.z];
        return default;
    }

    void RebuildDirty()
    {
        foreach (Vector3Int coord in _dirty)
        {
            if (_chunks.TryGetValue(coord, out TerrainChunk chunk))
                chunk.Rebuild(_grid, smoothShading);
        }
        _dirty.Clear();
    }

    /// <summary>Rebuild every chunk's mesh, regardless of dirty state. Call after seeding/loading the whole terrain.</summary>
    public void RebuildAll()
    {
        foreach (TerrainChunk chunk in _chunks.Values)
            chunk.Rebuild(_grid, smoothShading);
    }

    /// <summary>
    /// Reposition and remesh every existing chunk against the current <see cref="Grid"/> WITHOUT
    /// regenerating or resampling: the stored density/material arrays are kept as-is, only each
    /// chunk's local position and world sample spacing are restamped from the (possibly changed)
    /// grid. Used after a cell-size change — e.g. "Apply Transform" baking a GameObject scale into
    /// the terrain — so the already-sculpted terrain adopts the new world spacing losslessly.
    /// </summary>
    public void ReapplyGridToChunks()
    {
        foreach (KeyValuePair<Vector3Int, TerrainChunk> kv in _chunks)
        {
            TerrainChunk chunk = kv.Value;
            chunk.transform.localPosition = _grid.ChunkLocalPosition(kv.Key);
            // Same samples/Lod — just restamp this chunk's world spacing for its Lod.
            chunk.ApplyResampled(chunk.Density, chunk.Material, chunk.Lod, chunk.ChunkSize, _grid.SpacingAtLod(chunk.Lod));
        }
        RebuildAll();
    }

    // ---- Editor support (edit-mode sculpting) ------------------------------

    /// <summary>
    /// Bring the tool into a usable state without relying on <see cref="Awake"/>
    /// (which Unity only calls in Play mode): rebuild the grid and bind the paint
    /// texture array. Safe to call repeatedly; cheap enough to call from the editor
    /// sculpting tool each time it starts a session.
    /// </summary>
    public void EnsureInitialized()
    {
        ValidateConfig();
        BuildTextureArray();
    }

    /// <summary>Rebuild a single chunk's mesh now (used by edit-mode sculpt/undo so edits show immediately).</summary>
    public void RebuildChunk(Vector3Int coord)
    {
        if (_chunks.TryGetValue(coord, out TerrainChunk chunk))
            chunk.Rebuild(_grid, smoothShading);
    }

    /// <summary>
    /// Destroy every chunk GameObject and forget it. Also sweeps any orphaned child
    /// chunks not in the dictionary — e.g. edit-mode chunks that outlived a script
    /// recompile (which clears the runtime dictionary but keeps the GameObjects) —
    /// so "regenerate" always starts from a truly clean slate.
    /// </summary>
    public void ClearChunks()
    {
        foreach (TerrainChunk chunk in _chunks.Values)
            DestroyChunkObject(chunk);
        _chunks.Clear();
        _dirty.Clear();

        var orphans = GetComponentsInChildren<TerrainChunk>(includeInactive: true);
        foreach (TerrainChunk orphan in orphans)
            DestroyChunkObject(orphan);
    }

    /// <summary>Destroy one chunk by coordinate (used to undo a chunk that a stroke brought into existence). No-op if absent.</summary>
    public void DestroyChunk(Vector3Int coord)
    {
        if (!_chunks.TryGetValue(coord, out TerrainChunk chunk))
            return;
        _chunks.Remove(coord);
        _dirty.Remove(coord);
        DestroyChunkObject(chunk);
        MarkFaceNeighborsDirty(coord); // neighbours lose a stitched border; let them rebuild
    }

    static void DestroyChunkObject(TerrainChunk chunk)
    {
        if (chunk == null)
            return;
        if (Application.isPlaying)
            Destroy(chunk.gameObject);
        else
            DestroyImmediate(chunk.gameObject);
    }

    // ---- Initial world setup (merged from the former TerrainSetupTool) -------

    /// <summary>
    /// Seed the terrain: load <see cref="startupData"/> if set (falling back to the
    /// generated slab if it isn't a valid save), otherwise build the slab directly.
    /// Safe to call again at runtime or from the editor (e.g. a "Generate" button) —
    /// it clears any existing chunks first so repeated calls don't stack.
    /// </summary>
    public void GenerateOrLoad()
    {
        EnsureInitialized(); // Awake may not have run (edit mode) — make the grid/material valid first
        ClearChunks();       // start from a clean slate so repeated calls don't stack chunks

        bool loaded = startupData != null && startupData.bytes != null && startupData.bytes.Length > 0
            && LoadData(startupData.bytes);

        if (!loaded)
            GenerateInitialTerrain();

        RebuildAll();
    }

    /// <summary>
    /// Build a fresh starting slab, ignoring <see cref="startupData"/> entirely. Use this to get a
    /// clean, uniform-Lod canvas when a saved terrain has baked-in artifacts (e.g. broken chunk
    /// boundaries from an older version). The next auto-save/save will overwrite the saved data.
    /// </summary>
    public void GenerateFreshSlab()
    {
        EnsureInitialized();
        ClearChunks();
        GenerateInitialTerrain();
        RebuildAll();
    }

    void GenerateInitialTerrain()
    {
        Vector3Int origin = OriginChunk();
        for (int cx = 0; cx < initialChunks.x; cx++)
        for (int cy = 0; cy < initialChunks.y; cy++)
        for (int cz = 0; cz < initialChunks.z; cz++)
        {
            TerrainChunk chunk = GetOrCreateChunk(new Vector3Int(cx, cy, cz) + origin);
            FillSlab(chunk);
        }
    }

    /// <summary>
    /// Minimum chunk coordinate of the initial block, chosen so the generated region
    /// straddles the object's local origin (0,0,0) instead of growing into the +,+,+
    /// octant. For an even chunk count on an axis the region is centred exactly on 0;
    /// for an odd count it's off by half a chunk, which <see cref="FillSlab"/> accounts
    /// for by centring the slab on the region's true centre.
    /// </summary>
    Vector3Int OriginChunk() =>
        new Vector3Int(-initialChunks.x / 2, -initialChunks.y / 2, -initialChunks.z / 2);

    /// <summary>
    /// Fill a chunk with a thin, fully closed slab (a box signed-distance field).
    /// The SDF is evaluated per world position, so shared border samples match
    /// between neighbouring chunks.
    /// </summary>
    void FillSlab(TerrainChunk chunk)
    {
        TerrainGrid grid = _grid;
        float baseCellSize = grid.CellSize;

        // Region extent (manager-local world units) covered by the initial chunks. This uses
        // the fixed per-chunk footprint (base chunkSize/cellSize) — every chunk covers the same
        // world-space area regardless of its own Lod, only its internal sample count varies.
        Vector3 regionSize = (Vector3)(initialChunks * grid.ChunkSize) * baseCellSize;
        // The region no longer starts at the origin — it's shifted by OriginChunk so it straddles
        // (0,0,0). Centre the slab on the region's actual middle (≈ origin) rather than regionSize/2.
        Vector3 minCorner = (Vector3)(OriginChunk() * grid.ChunkSize) * baseCellSize;
        Vector3 center = minCorner + regionSize * 0.5f;

        // Box half-extents: wide in X/Z (minus the margin), thin in Y. Each axis is clamped so
        // there's always at least one air cell between the slab and the region edge — that gap is
        // what lets marching cubes cap the shape, so no margin/thickness value can leave it open.
        Vector3 half = new Vector3(
            ClosedHalfExtent(regionSize.x, regionSize.x * 0.5f - slabMargin, baseCellSize),
            ClosedHalfExtent(regionSize.y, slabThickness * 0.5f, baseCellSize),
            ClosedHalfExtent(regionSize.z, regionSize.z * 0.5f - slabMargin, baseCellSize));

        // Chunks are born coarse (defaultLod), so this fills far fewer samples than the base
        // resolution would — walk this chunk's own (possibly strided) local sample grid.
        int n = chunk.ChunkSize + 1;
        int stride = grid.StrideAtLod(chunk.Lod);
        for (int x = 0; x < n; x++)
        for (int y = 0; y < n; y++)
        for (int z = 0; z < n; z++)
        {
            Vector3 p = (Vector3)grid.SampleWorld(chunk.Coord, x * stride, y * stride, z * stride) * baseCellSize;
            float density = BoxSdf(p - center, half);
            chunk.Density[x, y, z] = density;
            // Seed a base texture layer on solid samples so the generated terrain renders textured.
            chunk.Material[x, y, z] = density < grid.IsoLevel ? MatSplat.Single(0) : MatSplat.Empty;
        }
    }

    /// <summary>
    /// A half-extent for one axis that keeps the slab strictly inside the region:
    /// at least one cell of solid, and always at least one cell of air before the
    /// edge, so the surface is capped rather than left open.
    /// </summary>
    static float ClosedHalfExtent(float regionSize, float desiredHalf, float cell)
    {
        float maxHalf = regionSize * 0.5f - cell; // leave one air cell at the edge
        return Mathf.Min(Mathf.Max(desiredHalf, cell), Mathf.Max(maxHalf, cell));
    }

    /// <summary>Signed distance to an axis-aligned box: negative inside (solid), positive outside (air).</summary>
    static float BoxSdf(Vector3 p, Vector3 half)
    {
        Vector3 q = new Vector3(
            Mathf.Abs(p.x) - half.x,
            Mathf.Abs(p.y) - half.y,
            Mathf.Abs(p.z) - half.z);

        float outside = Vector3.Max(q, Vector3.zero).magnitude;
        float inside = Mathf.Min(Mathf.Max(q.x, Mathf.Max(q.y, q.z)), 0f);
        return outside + inside;
    }

    // ---- View-driven render LOD (see TerrainLodController) ------------------

    /// <summary>Every live chunk (coord → chunk), so a view-distance controller can sweep them.</summary>
    public IEnumerable<KeyValuePair<Vector3Int, TerrainChunk>> Chunks => _chunks;

    /// <summary>Look up an existing chunk (e.g. for neighbour queries). False if none is built at that coord.</summary>
    public bool TryGetChunk(Vector3Int coord, out TerrainChunk chunk) => _chunks.TryGetValue(coord, out chunk);

    /// <summary>
    /// Set a chunk's RENDER Lod (mesh resolution) without touching its stored
    /// density, rebuilding only if it actually changed. Clamped to
    /// [storage Lod, maxLod]: a coarser mesh than the data is fine, a finer one is
    /// impossible. Because the coarser mesh is generated from a downsampled copy,
    /// lowering the render Lod again restores full detail — no sculpting is lost.
    /// </summary>
    public void SetChunkRenderLod(TerrainChunk chunk, int renderLod)
    {
        renderLod = Mathf.Clamp(renderLod, chunk.Lod, maxLod);
        if (renderLod == chunk.RenderLod)
            return;

        chunk.SetRenderLod(renderLod);
        chunk.Rebuild(_grid, smoothShading);
    }

    // ---- Save / load density data ------------------------------------------

    // "OPTV" — OnePen Terrain Voxels.
    const int SaveMagic = 0x4F505456;
    const int SaveVersion = 4;

    /// <summary>
    /// Serialize every chunk's density field to a compact binary blob. This is
    /// the true source of truth (what the brush edits), so a terrain loaded from
    /// it is fully sculptable — unlike a mesh/FBX, which is only the surface.
    /// Coarser chunks write proportionally fewer samples, so untouched regions
    /// cost little. As of v3 each chunk's density block is followed by its material
    /// (texture-paint) block.
    /// </summary>
    public byte[] SerializeData()
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);

        w.Write(SaveMagic);
        w.Write(SaveVersion);
        w.Write(chunkSize);
        w.Write(cellSize);
        w.Write(maxLod);
        w.Write(isoLevel);
        w.Write(_chunks.Count);

        foreach (KeyValuePair<Vector3Int, TerrainChunk> kv in _chunks)
        {
            Vector3Int c = kv.Key;
            TerrainChunk chunk = kv.Value;
            w.Write(c.x);
            w.Write(c.y);
            w.Write(c.z);
            w.Write(chunk.Lod);

            int n = chunk.ChunkSize + 1;
            float[,,] d = chunk.Density;
            for (int x = 0; x < n; x++)
            for (int y = 0; y < n; y++)
            for (int z = 0; z < n; z++)
                w.Write(d[x, y, z]);

            MatSplat[,,] mat = chunk.Material;
            for (int x = 0; x < n; x++)
            for (int y = 0; y < n; y++)
            for (int z = 0; z < n; z++)
                WriteSplat(w, mat[x, y, z]);
        }

        w.Flush();
        return ms.ToArray();
    }

    static void WriteSplat(BinaryWriter w, MatSplat s)
    {
        w.Write(s.w0); w.Write(s.w1); w.Write(s.w2); w.Write(s.w3);
    }

    static MatSplat ReadSplat(BinaryReader r)
    {
        MatSplat s;
        s.w0 = r.ReadSingle(); s.w1 = r.ReadSingle(); s.w2 = r.ReadSingle(); s.w3 = r.ReadSingle();
        return s;
    }

    /// <summary>Read a legacy v3 splat (4 index bytes + 4 weight floats) and fold it into the fixed-slot model.</summary>
    static MatSplat ReadSplatV3(BinaryReader r)
    {
        int i0 = r.ReadByte(), i1 = r.ReadByte(), i2 = r.ReadByte(), i3 = r.ReadByte();
        float w0 = r.ReadSingle(), w1 = r.ReadSingle(), w2 = r.ReadSingle(), w3 = r.ReadSingle();
        MatSplat s = default;
        AddSlice(ref s, i0, w0);
        AddSlice(ref s, i1, w1);
        AddSlice(ref s, i2, w2);
        AddSlice(ref s, i3, w3);
        return s;
    }

    static void AddSlice(ref MatSplat s, int index, float w)
    {
        if (w <= 0f)
            return;
        switch (Mathf.Clamp(index, 0, MatSplat.SlotCount - 1))
        {
            case 0: s.w0 += w; break;
            case 1: s.w1 += w; break;
            case 2: s.w2 += w; break;
            default: s.w3 += w; break;
        }
    }

    /// <summary>Default material for density loaded without a material block (v1/v2): a solid base layer where the sample is inside the surface, unpainted elsewhere.</summary>
    MatSplat[,,] DefaultMaterialFromDensity(float[,,] d, int n)
    {
        var mat = new MatSplat[n, n, n];
        for (int x = 0; x < n; x++)
        for (int y = 0; y < n; y++)
        for (int z = 0; z < n; z++)
            mat[x, y, z] = d[x, y, z] < isoLevel ? MatSplat.Single(0) : MatSplat.Empty;
        return mat;
    }

    /// <summary>
    /// Rebuild the chunk set from data produced by <see cref="SerializeData"/>.
    /// Returns false (and generates nothing) if the bytes aren't a valid save —
    /// callers decide what to fall back to.
    /// </summary>
    public bool LoadData(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        using var r = new BinaryReader(ms);

        if (r.ReadInt32() != SaveMagic)
        {
            Debug.LogError("[TerrainTool] Startup data is not a valid terrain save.");
            return false;
        }

        int version = r.ReadInt32();
        switch (version)
        {
            case 1: return LoadV1(r);
            case 2: return LoadV2(r);
            case 3: return LoadWithMaterial(r, ReadSplatV3); // legacy indexed splats, folded into fixed slots
            case 4: return LoadWithMaterial(r, ReadSplat);
            default:
                Debug.LogError($"[TerrainTool] Unsupported terrain save version {version}.");
                return false;
        }
    }

    /// <summary>Legacy format: one global chunkSize/cellSize, every chunk a full fixed-size array. Always loads as Lod 0.</summary>
    bool LoadV1(BinaryReader r)
    {
        int savedChunkSize = r.ReadInt32();
        float savedCellSize = r.ReadSingle();
        isoLevel = r.ReadSingle();
        if (!AdoptSavedGrid(savedChunkSize, savedCellSize, maxLod, v1: true))
            return false;
        int count = r.ReadInt32();

        int n = chunkSize + 1;
        for (int i = 0; i < count; i++)
        {
            var coord = new Vector3Int(r.ReadInt32(), r.ReadInt32(), r.ReadInt32());
            TerrainChunk chunk = GetOrCreateChunk(coord, lod: 0); // v1 saves are always full base resolution

            float[,,] d = chunk.Density;
            for (int x = 0; x < n; x++)
            for (int y = 0; y < n; y++)
            for (int z = 0; z < n; z++)
                d[x, y, z] = r.ReadSingle();
        }
        return true;
    }

    /// <summary>Current format: per-chunk Lod tag and a variable-size density block.</summary>
    bool LoadV2(BinaryReader r)
    {
        int savedChunkSize = r.ReadInt32();
        float savedCellSize = r.ReadSingle();
        int savedMaxLod = r.ReadInt32();
        isoLevel = r.ReadSingle();
        if (!AdoptSavedGrid(savedChunkSize, savedCellSize, savedMaxLod, v1: false))
            return false;
        int count = r.ReadInt32();

        for (int i = 0; i < count; i++)
        {
            var coord = new Vector3Int(r.ReadInt32(), r.ReadInt32(), r.ReadInt32());
            int lod = Mathf.Clamp(r.ReadInt32(), 0, maxLod);
            TerrainChunk chunk = GetOrCreateChunk(coord, lod);

            int chunkAtLod = _grid.CellsAtLod(lod);
            int n = chunkAtLod + 1;
            var d = new float[n, n, n];
            for (int x = 0; x < n; x++)
            for (int y = 0; y < n; y++)
            for (int z = 0; z < n; z++)
                d[x, y, z] = r.ReadSingle();

            // v2 has no material block — seed a base layer so it renders textured.
            chunk.ApplyResampled(d, DefaultMaterialFromDensity(d, n), lod, chunkAtLod, _grid.SpacingAtLod(lod));
        }
        return true;
    }

    /// <summary>v3/v4 format: v2 plus a per-chunk material (texture-paint) block after each density block. The splat reader varies by version.</summary>
    bool LoadWithMaterial(BinaryReader r, System.Func<BinaryReader, MatSplat> readSplat)
    {
        int savedChunkSize = r.ReadInt32();
        float savedCellSize = r.ReadSingle();
        int savedMaxLod = r.ReadInt32();
        isoLevel = r.ReadSingle();
        if (!AdoptSavedGrid(savedChunkSize, savedCellSize, savedMaxLod, v1: false))
            return false;
        int count = r.ReadInt32();

        for (int i = 0; i < count; i++)
        {
            var coord = new Vector3Int(r.ReadInt32(), r.ReadInt32(), r.ReadInt32());
            int lod = Mathf.Clamp(r.ReadInt32(), 0, maxLod);
            TerrainChunk chunk = GetOrCreateChunk(coord, lod);

            int chunkAtLod = _grid.CellsAtLod(lod);
            int n = chunkAtLod + 1;
            var d = new float[n, n, n];
            for (int x = 0; x < n; x++)
            for (int y = 0; y < n; y++)
            for (int z = 0; z < n; z++)
                d[x, y, z] = r.ReadSingle();

            var mat = new MatSplat[n, n, n];
            for (int x = 0; x < n; x++)
            for (int y = 0; y < n; y++)
            for (int z = 0; z < n; z++)
                mat[x, y, z] = readSplat(r);

            chunk.ApplyResampled(d, mat, lod, chunkAtLod, _grid.SpacingAtLod(lod));
        }
        return true;
    }

    /// <summary>
    /// Validate a saved grid against what the LOD system can represent, and adopt
    /// it if compatible. Voxel/density data can only be read back on the exact grid
    /// it was written with — the array sizes are derived from chunkSize/maxLod — so
    /// rather than force the current inspector grid onto mismatched data (which
    /// would misread the arrays and corrupt the terrain), we ADOPT the saved
    /// chunkSize/cellSize/maxLod and just warn. The inspector cellSize/chunkSize is
    /// therefore ignored when startupData is present; the terrain loads at its
    /// authored resolution.
    ///
    /// If the saved grid can't be represented at all (chunk size not a power of two,
    /// or maxLod out of range — only possible from a corrupt or hand-edited file,
    /// never from <see cref="SerializeData"/>), we refuse the load and change
    /// nothing, so the caller falls back to generation instead of producing garbage.
    /// </summary>
    bool AdoptSavedGrid(int savedChunkSize, float savedCellSize, int savedMaxLod, bool v1)
    {
        int validChunkSize = Mathf.Clamp(Mathf.ClosestPowerOfTwo(Mathf.Max(savedChunkSize, 2)), 2, MaxChunkSize);
        bool maxLodOk = v1 || savedMaxLod == Mathf.Clamp(savedMaxLod, 0, TerrainGrid.MaxLodFor(savedChunkSize));
        if (validChunkSize != savedChunkSize || !maxLodOk)
        {
            Debug.LogError($"[TerrainTool] Saved terrain grid (chunkSize={savedChunkSize}" +
                           (v1 ? "" : $", maxLod={savedMaxLod}") + $") can't be represented by the LOD system " +
                           $"(chunk size must be a power of two in [2, {MaxChunkSize}] and maxLod within range). " +
                           "Skipping load — the terrain will fall back to generation.");
            return false;
        }

        if (savedChunkSize != chunkSize || !Mathf.Approximately(savedCellSize, cellSize))
        {
            Debug.LogWarning($"[TerrainTool] startupData ({(v1 ? "v1" : "v2")}) was saved with chunkSize={savedChunkSize}, " +
                              $"cellSize={savedCellSize}, which differs from the current inspector values " +
                              $"({chunkSize}, {cellSize}) — adopting the saved grid so the data lines up.");
        }

        chunkSize = savedChunkSize;
        cellSize = savedCellSize;
        if (!v1)
            maxLod = savedMaxLod;
        ValidateConfig(); // rebuild _grid; a no-op for chunkSize/maxLod here, but sanitizes a bad cellSize
        return true;
    }

    // ---- Export helpers ----------------------------------------------------

    /// <summary>
    /// Merge every chunk's mesh into a single new mesh, expressed in this
    /// object's local space. The result is a standalone, reusable mesh — it
    /// shares nothing with the live chunk meshes, so it's safe to save as an
    /// asset or hand to the FBX exporter. Only meaningful while playing (chunks
    /// are generated at runtime). Returns null if there's no geometry.
    /// </summary>
    public Mesh CombineToMesh()
    {
        var combines = new List<CombineInstance>();
        foreach (TerrainChunk chunk in _chunks.Values)
        {
            if (!chunk.TryGetComponent(out MeshFilter mf) || mf.sharedMesh == null || mf.sharedMesh.vertexCount == 0)
                continue;

            combines.Add(new CombineInstance
            {
                mesh = mf.sharedMesh,
                transform = transform.worldToLocalMatrix * chunk.transform.localToWorldMatrix
            });
        }

        if (combines.Count == 0)
            return null;

        var combined = new Mesh
        {
            name = "TerrainMesh",
            indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
        };
        combined.CombineMeshes(combines.ToArray(), mergeSubMeshes: true, useMatrices: true);
        combined.RecalculateNormals();
        combined.RecalculateBounds();
        return combined;
    }

    // ---- Chunk creation & seam bookkeeping ---------------------------------

    /// <summary>Get the chunk at a coordinate, creating (at lod, default defaultLod) and border-stitching it on demand. Used by sculpting, save/load, and initial generation.</summary>
    public TerrainChunk GetOrCreateChunk(Vector3Int coord, int lod = -1)
    {
        if (_chunks.TryGetValue(coord, out TerrainChunk existing))
            return existing;

        FireBeforeEdit(coord); // about to come into existence — snapshot it as "didn't exist"

        lod = Mathf.Clamp(lod < 0 ? defaultLod : lod, 0, maxLod);

        var go = new GameObject($"Chunk_{coord.x}_{coord.y}_{coord.z}");
        // In edit mode the terrain is presented as a single object: the chunks still carry
        // the meshes/colliders that sculpting and rendering need, but they're hidden from the
        // hierarchy (and kept out of the saved scene, since their density isn't serialized —
        // save to a .bytes asset to keep it). At runtime (Play) they're ordinary visible
        // GameObjects so culling/LOD can work per chunk.
        if (!Application.isPlaying)
            go.hideFlags = HideFlags.DontSaveInEditor | HideFlags.HideInHierarchy;
        go.transform.SetParent(transform, false);
        go.transform.localPosition = _grid.ChunkLocalPosition(coord);

        var chunk = go.AddComponent<TerrainChunk>();
        EnsureMaterial(); // a chunk object always loads the terrain material in, even if none was assigned
        if (go.TryGetComponent(out MeshRenderer mr) && chunkMaterial != null)
            mr.sharedMaterial = chunkMaterial;

        chunk.Init(_grid, coord, lod);
        InitBorders(chunk); // copy shared samples from any existing neighbours

        _chunks.Add(coord, chunk);
        MarkFaceNeighborsDirty(coord); // a neighbour's transition-cell decision depends on this chunk's existence
        return chunk;
    }

    /// <summary>
    /// Seed a freshly created chunk: air everywhere, except border samples that
    /// an already-existing neighbour also owns (at any Lod) are copied from that
    /// neighbour so the two surfaces stay stitched together wherever they can be.
    /// </summary>
    void InitBorders(TerrainChunk chunk)
    {
        int n = chunk.ChunkSize + 1;
        int stride = _grid.StrideAtLod(chunk.Lod);
        float air = _grid.AirDensity;
        for (int x = 0; x < n; x++)
        for (int y = 0; y < n; y++)
        for (int z = 0; z < n; z++)
        {
            float value = air;

            bool onBorder = x == 0 || y == 0 || z == 0 ||
                            x == chunk.ChunkSize || y == chunk.ChunkSize || z == chunk.ChunkSize;
            if (onBorder)
            {
                Vector3Int ws = _grid.SampleWorld(chunk.Coord, x * stride, y * stride, z * stride);
                if (TryGetExistingSample(ws, chunk.Coord, int.MaxValue, out float shared))
                    value = shared;
                if (TryGetExistingMaterialSample(ws, chunk.Coord, int.MaxValue, out MatSplat sharedMat))
                    chunk.Material[x, y, z] = sharedMat; // interior stays MatSplat.Empty (unpainted)
            }

            chunk.Density[x, y, z] = value;
        }
    }

    /// <summary>
    /// Look up a world sample in some already-existing chunk (not <paramref name="exclude"/>)
    /// whose Lod is at most <paramref name="maxLodInclusive"/> and that actually
    /// stores this exact sample — preferring the finest such match.
    /// </summary>
    bool TryGetExistingSample(Vector3Int ws, Vector3Int exclude, int maxLodInclusive, out float value)
    {
        SampleOwners(ws);

        bool found = false;
        int bestLod = int.MaxValue;
        float bestValue = 0f;
        foreach (var owner in _owners)
        {
            if (owner.Key == exclude)
                continue;
            if (!_chunks.TryGetValue(owner.Key, out TerrainChunk chunk))
                continue;
            if (chunk.Lod > maxLodInclusive || chunk.Lod >= bestLod)
                continue;
            if (!_grid.TryLocalIndex(chunk.Lod, owner.Value, out Vector3Int local))
                continue;

            bestLod = chunk.Lod;
            bestValue = chunk.Density[local.x, local.y, local.z];
            found = true;
        }

        value = bestValue;
        return found;
    }

    /// <summary>Material-field sibling of <see cref="TryGetExistingSample"/>: the material splat of the finest existing chunk (other than <paramref name="exclude"/>) that stores this exact sample.</summary>
    bool TryGetExistingMaterialSample(Vector3Int ws, Vector3Int exclude, int maxLodInclusive, out MatSplat value)
    {
        SampleOwners(ws);

        bool found = false;
        int bestLod = int.MaxValue;
        MatSplat bestValue = default;
        foreach (var owner in _owners)
        {
            if (owner.Key == exclude)
                continue;
            if (!_chunks.TryGetValue(owner.Key, out TerrainChunk chunk))
                continue;
            if (chunk.Lod > maxLodInclusive || chunk.Lod >= bestLod)
                continue;
            if (!_grid.TryLocalIndex(chunk.Lod, owner.Value, out Vector3Int local))
                continue;

            bestLod = chunk.Lod;
            bestValue = chunk.Material[local.x, local.y, local.z];
            found = true;
        }

        value = bestValue;
        return found;
    }

    // ---- Coordinate helpers ------------------------------------------------

    /// <summary>World-sample lattice coordinate (base/Lod-0 units) of a chunk-relative local sample index. Used by sculpting and by initial-terrain generation.</summary>
    public Vector3Int SampleWorld(Vector3Int coord, int lx, int ly, int lz) => _grid.SampleWorld(coord, lx, ly, lz);

    /// <summary>
    /// Fill <see cref="_owners"/> with every (chunkCoord, localBaseIndex) pair
    /// that COULD store world sample <paramref name="ws"/> — i.e. every chunk
    /// whose fixed footprint contains it, in base-lattice-index terms (0..chunkSize).
    /// Chunk partitioning is uniform regardless of Lod, so this candidate
    /// discovery is unaffected by resolution; whether a given candidate actually
    /// stores this exact sample (its own Lod might be too coarse) is decided
    /// separately via <see cref="TerrainGrid.TryLocalIndex"/>.
    /// </summary>
    void SampleOwners(Vector3Int ws)
    {
        int cx0, lx0, cx1, lx1; int nx = AxisOwners(ws.x, out cx0, out lx0, out cx1, out lx1);
        int cy0, ly0, cy1, ly1; int ny = AxisOwners(ws.y, out cy0, out ly0, out cy1, out ly1);
        int cz0, lz0, cz1, lz1; int nz = AxisOwners(ws.z, out cz0, out lz0, out cz1, out lz1);

        _owners.Clear();
        for (int ix = 0; ix < nx; ix++)
        for (int iy = 0; iy < ny; iy++)
        for (int iz = 0; iz < nz; iz++)
        {
            int cxx = ix == 0 ? cx0 : cx1, lxx = ix == 0 ? lx0 : lx1;
            int cyy = iy == 0 ? cy0 : cy1, lyy = iy == 0 ? ly0 : ly1;
            int czz = iz == 0 ? cz0 : cz1, lzz = iz == 0 ? lz0 : lz1;

            _owners.Add(new KeyValuePair<Vector3Int, Vector3Int>(
                new Vector3Int(cxx, cyy, czz),
                new Vector3Int(lxx, lyy, lzz)));
        }
    }

    /// <summary>
    /// Candidate owning chunk(s) of a base-lattice world-sample coordinate along
    /// one axis (in terms of the fixed, Lod-independent chunk partitioning).
    /// Returns 1 normally, or 2 when the sample sits exactly on a chunk boundary
    /// (owned by both the chunk starting there and the previous chunk's far
    /// border).
    /// </summary>
    int AxisOwners(int w, out int cc0, out int local0, out int cc1, out int local1)
    {
        int cc = Mathf.FloorToInt((float)w / chunkSize);
        int local = w - cc * chunkSize; // 0 .. chunkSize-1

        cc0 = cc;
        local0 = local;

        if (local == 0)
        {
            cc1 = cc - 1;
            local1 = chunkSize;
            return 2;
        }

        cc1 = 0;
        local1 = 0;
        return 1;
    }
}
