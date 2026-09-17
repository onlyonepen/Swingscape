using JL.Splitting;
using UnityEngine;

[CreateAssetMenu(fileName = "New Slice Attack Type", menuName = "Swingscape/Attack Types/Slice")]
public class SliceAttackTypeSO : AttackTypeSO
{
    [Tooltip("Impulse (along the cut plane's normal) used when a Splittable has to be added on the fly. Ignored for objects that already have their own Splittable configured.")]
    [SerializeField] private float splitForce = 4f;
    [Tooltip("Should freshly added Splittables generate mesh colliders for the resulting halves?")]
    [SerializeField] private bool generateMeshColliders = true;
    [SerializeField] private Material InsideMaterial;

    public override void Apply(AttackContext ctx)
    {
        var splittable = ctx.HitObject.GetComponentInParent<Splittable>();
        if (splittable == null)
        {
            splittable = TryAddSplittable(ctx.HitObject);
        }
        if (splittable == null || !ctx.SlicedThisSwing.Add(splittable)) return;

        var sliceable = splittable.GetComponent<ISliceable>();
        sliceable?.OnSliceStart();

        var plane = new PointPlane(ctx.ImpactPlane.position, ctx.ImpactPlane.rotation);
        splittable.CapMaterial =  InsideMaterial;
        splittable.SplitAsync(plane, result =>
        {
            if (result.posObject == null || result.negObject == null) return;

            result.posObject.transform.parent = null;
            result.negObject.transform.parent = null;

            sliceable?.OnSliceComplete(result);
        });
    }

    // Lets any object with a readable mesh be sliced even if it was never set up with a
    // Splittable component in the editor (e.g. a plain cube).
    private Splittable TryAddSplittable(GameObject hitObject)
    {
        var meshFilter = hitObject.GetComponentInParent<MeshFilter>();
        if (meshFilter == null) return null;

        var splittable = meshFilter.gameObject.AddComponent<Splittable>();
        splittable.targetMeshFilter = meshFilter;
        splittable.SplitForce = splitForce;
        splittable.generateMeshColliders = generateMeshColliders;
        return splittable;
    }
}
