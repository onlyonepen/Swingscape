using UnityEngine;

/// <summary>Knockback that retargets: searches the same aim-assist cone the grapple uses
/// (against targetLayers instead of Grappleable components) for a nearby target along the
/// knockback direction. If one is found, launches the hit Rigidbody straight at it (strength
/// from retargetForce); otherwise falls back to a plain impulse straight away from the
/// attacker (strength from force).</summary>
[CreateAssetMenu(fileName = "New Knockback Attack Type", menuName = "Swingscape/Attack Types/Knockback")]
public class KnockbackAttackTypeSO : AttackTypeSO
{
    [Tooltip("Impulse force applied to the hit Rigidbody when no auto-aim target is found")]
    [SerializeField] private float force = 10f;

    [Header("Auto-Aim Retarget")]
    [Tooltip("Layers searched for a nearby target to launch the hit object toward. Add more layers here later as needed.")]
    [SerializeField] private LayerMask targetLayers;

    [Tooltip("Max distance to search for a retarget along the knockback direction")]
    [SerializeField] private float retargetMaxDistance = 15f;

    [Tooltip("Aim-assist cone radius near the knocked-back object (same idea as grapple aim assist)")]
    [SerializeField] private float retargetMinRadius = 0.8f;

    [Tooltip("Aim-assist cone radius at max distance")]
    [SerializeField] private float retargetMaxRadius = 5f;

    [Tooltip("Impulse force applied when a retarget is found (direction is straight at the target)")]
    [SerializeField] private float retargetForce = 10f;

    [Header("Knockback Damage")]
    [Tooltip("Speed (m/s) the knocked-back Rigidbody needs to start dealing collision damage")]
    [SerializeField] private float damageArmSpeed = 5f;

    [Tooltip("Speed (m/s) below which the disarm countdown starts")]
    [SerializeField] private float damageDisarmSpeed = 1f;

    [Tooltip("Seconds spent below the disarm speed before losing the damaging state")]
    [SerializeField] private float damageDisarmDelay = 2f;

    [Header("Debug")]
    [Tooltip("Log whether a retarget was found (and draw it in Scene view) every time this attack lands")]
    [SerializeField] private bool debugAim = false;

    public override void Apply(AttackContext ctx)
    {
        if (!(ctx.HitObject.GetComponentInParent<Rigidbody>() is Rigidbody rb) || !ctx.KnockedBackThisSwing.Add(rb))
            return;

        rb.linearVelocity = Vector3.zero;
        
        Vector3 awayFromAttacker = (rb.transform.position - ctx.AttackerPosition).normalized;

        bool foundTarget = AimAssist.TryFindTarget(
            rb.transform.position, awayFromAttacker, retargetMaxDistance,
            retargetMinRadius, retargetMaxRadius, targetLayers, GlobalReference.Instance.TerrainLayer,
            candidate => candidate != rb.gameObject,
            out RaycastHit hit);

        if (foundTarget)
        {
            Vector3 towardTarget = (hit.point - rb.transform.position).normalized;
            rb.AddForce(towardTarget * retargetForce, ForceMode.Impulse);

            if (debugAim)
            {
                Debug.Log($"[Knockback] {rb.name} retargeted onto {hit.collider.name} at {hit.point} (distance {Vector3.Distance(rb.transform.position, hit.point):F1}m)");
                Debug.DrawLine(rb.transform.position, hit.point, Color.green, 2f);
            }
        }
        else
        {
            // No target in range/cone: knock forward in the attacker's look direction instead.
            Vector3 lookDirection = GlobalReference.Instance.player.Cam.transform.forward;
            rb.AddForce(lookDirection * force, ForceMode.Impulse);

            if (debugAim)
            {
                Debug.Log($"[Knockback] {rb.name} found no retarget in {targetLayers.value} within {retargetMaxDistance}m, falling back to forward knockback");
                Debug.DrawRay(rb.transform.position, lookDirection * retargetMaxDistance, Color.red, 2f);
            }
        }

        // Only objects actually hit by a Knockback attack ever get this component.
        if (!rb.TryGetComponent(out KnockbackDamage damage))
            damage = rb.gameObject.AddComponent<KnockbackDamage>();
        damage.Configure(damageArmSpeed, damageDisarmSpeed, damageDisarmDelay);
    }
}
