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
        // Look this up from the hit object itself (not from the Splittable) so it still resolves
        // even when we end up splitting a disposable visual copy instead of the real object below.
        var sliceable = ctx.HitObject.GetComponentInParent<ISliceable>();

        var splittable = ctx.HitObject.GetComponentInParent<Splittable>();
        if (splittable == null)
        {
            splittable = TryAddSplittable(ctx.HitObject, sliceable);
        }
        if (splittable == null || !ctx.SlicedThisSwing.Add(splittable)) return;

        sliceable?.OnSliceStart();

        var plane = new PointPlane(ctx.ImpactPlane.position, ctx.ImpactPlane.rotation);
        splittable.CapMaterial =  InsideMaterial;
        splittable.SplitAsync(plane, result =>
        {
            // The cut plane can miss the mesh entirely (e.g. a glancing hit), leaving both
            // objects null. ownerSliceable already committed to dying in OnSliceStart, so still
            // notify it here (with whatever we got) instead of leaving it stuck mid-death forever.
            if (result.posObject != null)
            {
                result.posObject.transform.parent = null;
                result.posObject.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.None;
            }
            if (result.negObject != null)
            {
                result.negObject.transform.parent = null;
                result.negObject.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.None;
            }

            sliceable?.OnSliceComplete(result);
        });
    }

    // Lets any object with a readable mesh be sliced even if it was never set up with a
    // Splittable component in the editor (e.g. a plain cube).
    //
    // The plugin splits by instantiating rootObject as one half and reusing rootObject in place
    // as the other, so if we added the Splittable straight onto ownerSliceable's own GameObject
    // (e.g. an enemy), both resulting halves would carry a full copy of every gameplay component
    // on it (AI, rigidbody, colliders...) instead of just being mesh debris. When an ISliceable
    // owns the mesh, split a disposable visual-only copy instead and let ownerSliceable's
    // OnSliceStart/OnSliceComplete drive the real object's death/cleanup.
    private Splittable TryAddSplittable(GameObject hitObject, ISliceable ownerSliceable)
    {
        var meshFilter = hitObject.GetComponentInParent<MeshFilter>();
        if (meshFilter == null) return null;

        if (ownerSliceable != null)
        {
            var originalRenderer = meshFilter.GetComponent<Renderer>();
            var originalRb = meshFilter.GetComponentInParent<Rigidbody>();

            var visualCopy = new GameObject(meshFilter.gameObject.name + " (Sliced)");
            visualCopy.transform.SetPositionAndRotation(meshFilter.transform.position, meshFilter.transform.rotation);
            visualCopy.transform.localScale = meshFilter.transform.lossyScale;

            var copyFilter = visualCopy.AddComponent<MeshFilter>();
            copyFilter.sharedMesh = meshFilter.sharedMesh;

            if (originalRenderer != null)
            {
                visualCopy.AddComponent<MeshRenderer>().sharedMaterials = originalRenderer.sharedMaterials;
                originalRenderer.enabled = false;
            }

            var copyRb = visualCopy.AddComponent<Rigidbody>();
            if (originalRb != null)
            {
                copyRb.mass = originalRb.mass;
                copyRb.useGravity = originalRb.useGravity;
            }

            // Let the detached debris keep getting hit/sliced further, same as a plain prop.
            visualCopy.AddComponent<Attackable>().AttackType = this;

            meshFilter = copyFilter;
        }

        var splittable = meshFilter.gameObject.AddComponent<Splittable>();
        splittable.targetMeshFilter = meshFilter;
        splittable.SplitForce = splitForce;
        splittable.generateMeshColliders = generateMeshColliders;
        return splittable;
    }
}
