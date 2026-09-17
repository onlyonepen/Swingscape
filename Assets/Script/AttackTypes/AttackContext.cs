using System.Collections.Generic;
using JL.Splitting;
using UnityEngine;

/// <summary>Everything an AttackTypeSO needs to resolve a single hit. Built once per swing by
/// the attacker and passed into Attackable.ApplyAttack() so each target's type-specific
/// behavior (slice, knockback, ...) can run without the attacker knowing the specifics.</summary>
public readonly struct AttackContext
{
    public readonly GameObject HitObject;
    public readonly Vector3 AttackerPosition;
    public readonly Transform ImpactPlane;

    // Dedup sets shared across the whole swing so multiple child colliders on the same
    // target don't double-apply slicing/knockback.
    public readonly HashSet<Splittable> SlicedThisSwing;
    public readonly HashSet<Rigidbody> KnockedBackThisSwing;

    public AttackContext(GameObject hitObject, Vector3 attackerPosition, Transform impactPlane,
        HashSet<Splittable> slicedThisSwing, HashSet<Rigidbody> knockedBackThisSwing)
    {
        HitObject = hitObject;
        AttackerPosition = attackerPosition;
        ImpactPlane = impactPlane;
        SlicedThisSwing = slicedThisSwing;
        KnockedBackThisSwing = knockedBackThisSwing;
    }
}
