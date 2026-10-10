using UnityEngine;

/// <summary>
/// PART 2b — Per-sample texture weights for painting (fixed-slot splatting).
///
/// The companion to the density field: where density says WHERE the surface is,
/// this says WHAT texture it shows. It holds four weights, one per FIXED
/// <c>Texture2DArray</c> slice (0..3). Because the slot→slice mapping is fixed and
/// global, only weights vary between samples — never an index — so the GPU can
/// interpolate them across a triangle and textures cross-fade smoothly instead of
/// hard-switching. (Per-vertex indices can't be interpolated: a value halfway
/// between slice 0 and 1 would just round to a whole slice, giving a hard edge —
/// which is why this model uses fixed slots.)
///
/// Weights are kept roughly normalized to sum to 1 (0 everywhere = untouched, which
/// the shader renders as slice 0 so sculpted-but-unpainted ground still shows a
/// base texture). Pure data + math (no Unity mesh dependency), matching
/// <see cref="CubeMarching"/> and <see cref="LodResample"/>.
/// </summary>
public struct MatSplat
{
    // Weight of each fixed texture-array slice (0..3). Slot k always means slice k.
    public float w0, w1, w2, w3;

    /// <summary>Unpainted sample: no weight. Renders as slice 0 via the shader's fallback.</summary>
    public static readonly MatSplat Empty = default;

    /// <summary>True when no slice carries any weight (a never-assigned sample).</summary>
    public readonly bool IsEmpty => w0 <= 0f && w1 <= 0f && w2 <= 0f && w3 <= 0f;

    /// <summary>Number of texture slots (vertex-color channels) available for blending.</summary>
    public const int SlotCount = 4;

    /// <summary>A sample fully covered by a single texture slice.</summary>
    public static MatSplat Single(int slice)
    {
        MatSplat s = default;
        switch (Mathf.Clamp(slice, 0, SlotCount - 1))
        {
            case 0: s.w0 = 1f; break;
            case 1: s.w1 = 1f; break;
            case 2: s.w2 = 1f; break;
            default: s.w3 = 1f; break;
        }
        return s;
    }

    readonly float Get(int slice) => slice == 0 ? w0 : slice == 1 ? w1 : slice == 2 ? w2 : w3;

    /// <summary>
    /// Raise <paramref name="slice"/>'s (normalized) weight TOWARD <paramref name="target"/>
    /// by fraction <paramref name="rate"/>, shrinking the other slices to fill the
    /// rest. Painting only ever raises, and it stops once the weight reaches the
    /// target — so a solid-core / tapered-rim brush profile makes the interior become
    /// fully this texture while the rim settles at a partial weight that cross-fades
    /// into the surrounding textures, no matter how long the brush is held.
    /// </summary>
    /// <param name="target">Desired normalized weight here (the brush profile: 1 in the core, tapering at the rim).</param>
    /// <param name="rate">How far to move toward the target this step (strength * deltaTime), 0..1.</param>
    public static void Paint(ref MatSplat s, int slice, float target, float rate)
    {
        target = Mathf.Clamp01(target);
        rate = Mathf.Clamp01(rate);
        if (target <= 0f || rate <= 0f)
            return;

        slice = Mathf.Clamp(slice, 0, SlotCount - 1);
        float current = s.Get(slice);
        if (current >= target)
            return; // already at/above the profile here — painting never reduces

        float newWeight = current + (target - current) * rate;

        // Scale the other slices to occupy the remaining (1 - newWeight), keeping ratios.
        float others = (s.w0 + s.w1 + s.w2 + s.w3) - current;
        float scale = others > 1e-6f ? (1f - newWeight) / others : 0f;

        s.w0 = slice == 0 ? newWeight : s.w0 * scale;
        s.w1 = slice == 1 ? newWeight : s.w1 * scale;
        s.w2 = slice == 2 ? newWeight : s.w2 * scale;
        s.w3 = slice == 3 ? newWeight : s.w3 * scale;
    }

    /// <summary>
    /// Interpolate two splats — a plain per-slice lerp, since the slot→slice mapping
    /// is fixed. Used for marching-cubes edge vertices and LOD refine.
    /// </summary>
    public static MatSplat Lerp(in MatSplat a, in MatSplat b, float t)
    {
        MatSplat r;
        r.w0 = Mathf.Lerp(a.w0, b.w0, t);
        r.w1 = Mathf.Lerp(a.w1, b.w1, t);
        r.w2 = Mathf.Lerp(a.w2, b.w2, t);
        r.w3 = Mathf.Lerp(a.w3, b.w3, t);
        return r;
    }

    /// <summary>Pack for the mesh: the four slice weights into the vertex color (RGBA).</summary>
    public readonly void WriteAttributes(out Color weights)
    {
        weights = new Color(w0, w1, w2, w3);
    }
}
