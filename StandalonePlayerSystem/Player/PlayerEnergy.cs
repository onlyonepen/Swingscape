using Script.Enemy;
using UnityEngine;

/// <summary>
/// Owns the player's energy pool: storage, spending and regeneration math.
/// The locomotion state machine decides the regen *rate* (it knows the state)
/// and pushes it in each frame via Regen(); this component holds the *data*.
/// </summary>
public class PlayerEnergy : MonoBehaviour
{
    public bool useEnergy = true;
    public float MaxEnergy = 100f;

    [Header("Usage costs")]
    public float InitialThrowUsage = 20;
    public float GrappleLeapUsage = 40;
    public float GrappleDashUsage = 10;

    [Header("Regeneration")]
    public float EnergyRegeneration = 5f;
    public float GroundedEnergyRegeneration = 50f;

    public float currentEnergy;

    // Decoupled from the enemy system: refill when the host project reports a target
    // killed via PlayerCombatEvents.RaiseTargetKilled() (was BaseEnemy.OnAnyEnemyDied).
    private void OnEnable()  => PlayerCombatEvents.OnTargetKilled += Refill;
    private void OnDisable() => PlayerCombatEvents.OnTargetKilled -= Refill;

    private void Start()
    {
        currentEnergy = MaxEnergy;
    }

    /// <summary>Tries to spend energy. Returns true if the action is allowed.</summary>
    public bool UseEnergy(float usage)
    {
        // NOTE: energy consumption is currently bypassed (preserved from the original
        // implementation). Remove this early return when you want costs to apply.
        return true;

        if (!useEnergy) return true;

        if (currentEnergy - usage >= 0)
        {
            currentEnergy -= usage;
            return true;
        }
        return false;
    }

    /// <summary>Refill to full (e.g. on enemy kill).</summary>
    public void Refill()
    {
        currentEnergy = MaxEnergy;
    }

    /// <summary>Accumulate energy at the given per-second rate, clamped to MaxEnergy.</summary>
    public void Regen(float ratePerSecond)
    {
        currentEnergy = Mathf.Min(currentEnergy + ratePerSecond * Time.deltaTime, MaxEnergy);
    }
}
