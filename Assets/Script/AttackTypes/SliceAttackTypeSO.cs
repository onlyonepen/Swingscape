using UnityEngine;

[CreateAssetMenu(fileName = "New Slice Attack Type", menuName = "Swingscape/Attack Types/Slice")]
public class SliceAttackTypeSO : AttackTypeSO
{
    public override void Apply(AttackContext ctx)
    {
        var damagable = ctx.HitObject.GetComponentInParent<IDamagable>();
        if (damagable != null && ctx.DamagedThisSwing.Add(damagable))
        {
            damagable.SplitDeath(ctx.ImpactPlane);
        }
    }
}
