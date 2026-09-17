using UnityEngine;

[CreateAssetMenu(fileName = "New Knockback Attack Type", menuName = "Swingscape/Attack Types/Knockback")]
public class KnockbackAttackTypeSO : AttackTypeSO
{
    [Tooltip("Impulse force applied to the hit Rigidbody")]
    [SerializeField] private float force = 10f;

    public override void Apply(AttackContext ctx)
    {
        if (ctx.HitObject.GetComponentInParent<Rigidbody>() is Rigidbody rb && ctx.KnockedBackThisSwing.Add(rb))
        {
            Vector3 dir = (rb.transform.position - ctx.AttackerPosition).normalized;
            rb.AddForce(dir * force, ForceMode.Impulse);
        }
    }
}
