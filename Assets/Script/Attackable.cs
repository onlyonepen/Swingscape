using System.Collections.Generic;
using UnityEngine;

public class Attackable : MonoBehaviour
{
    [Tooltip("Per-target attack resolution strategy (slice, knockback, ...)")]
    [SerializeField] private AttackTypeSO attackType;
    public AttackTypeSO AttackType { get => attackType; set => attackType = value; }

    [Tooltip("Hit-reaction effects (slow-mo, hit-stop, ...) fired when this object is hit or grappled")]
    [SerializeField] private List<HitEffectSO> hitEffects = new List<HitEffectSO>();

    /// <summary>Resolves this hit via the configured AttackTypeSO (slice, knockback, ...).
    /// No-ops if no attack type is assigned.</summary>
    public void ApplyAttack(AttackContext ctx) => attackType?.Apply(ctx);

    /// <summary>Copies this Attackable's attack type and hit effects onto another instance -
    /// used to make an owner's detached pieces independently attackable after it dies.</summary>
    public void CopyTo(Attackable target)
    {
        target.attackType = attackType;
        target.hitEffects = new List<HitEffectSO>(hitEffects);
    }

    /// <summary>Fires every configured hit effect against this object. Called on melee impact
    /// and on grapple connect so both flows share the same effect configuration.</summary>
    public void TriggerHitEffects()
    {
        foreach (HitEffectSO effect in hitEffects)
        {
            if (effect != null) effect.Trigger(gameObject);
        }
    }
}
