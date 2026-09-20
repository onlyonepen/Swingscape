using System.Collections.Generic;
using JL.Splitting;
using UnityEngine;

/// <summary>Runtime-only: attached by KnockbackAttackTypeSO to whatever Rigidbody it just
/// knocked back, so that object can damage other Attackables it collides with while still
/// moving fast. Never add this manually — objects without a Knockback attack type never get one.</summary>
[RequireComponent(typeof(Rigidbody))]
public class KnockbackDamage : MonoBehaviour
{
    private float armSpeed;
    private float disarmSpeed;
    private float disarmDelay;

    private Rigidbody rb;
    private bool isArmed;
    private float belowDisarmSpeedTimer;

    /// <summary>Called by KnockbackAttackTypeSO every time it applies an impulse, so tuning
    /// always reflects that attack type asset's current values.</summary>
    public void Configure(float armSpeed, float disarmSpeed, float disarmDelay)
    {
        this.armSpeed = armSpeed;
        this.disarmSpeed = disarmSpeed;
        this.disarmDelay = disarmDelay;
    }

    private void Awake() => rb = GetComponent<Rigidbody>();

    private void FixedUpdate()
    {
        float speed = rb.linearVelocity.magnitude;

        if (speed >= armSpeed)
        {
            isArmed = true;
            belowDisarmSpeedTimer = 0f;
            return;
        }

        if (!isArmed) return;

        if (speed < disarmSpeed)
        {
            belowDisarmSpeedTimer += Time.fixedDeltaTime;
            if (belowDisarmSpeedTimer >= disarmDelay) isArmed = false;
        }
        else
        {
            belowDisarmSpeedTimer = 0f;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!isArmed) return;

        Attackable other = collision.collider.GetComponentInParent<Attackable>();
        if (other == null || other.gameObject == gameObject) return;

        other.TriggerHitEffects();

        var ctx = new AttackContext(other.gameObject, transform.position, transform,
            new HashSet<Splittable>(), new HashSet<Rigidbody>());
        other.ApplyAttack(ctx);
    }
}
