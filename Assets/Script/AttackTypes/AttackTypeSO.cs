using UnityEngine;

/// <summary>Base class for a per-target attack resolution strategy (slice, knockback, ...).
/// Concrete types live as reusable assets, mirroring HitEffectSO.</summary>
public abstract class AttackTypeSO : ScriptableObject
{
    public abstract void Apply(AttackContext ctx);
}
