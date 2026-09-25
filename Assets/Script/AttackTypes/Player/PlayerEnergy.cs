using Script.Enemy;
using UnityEngine;

/// <summary>
/// Owns the player's energy pool: storage, spending and regeneration math.
/// The locomotion state machine decides the regen *rate* (it knows the state)
/// and pushes it in each frame via Regen(); this component holds the *data*.
/// </summary>
public class PlayerEnergy : MonoBehaviour
{
    public PlayerEnergyStatsSO stats;

    public float currentEnergy;

    private void OnEnable()  => BaseEnemy.OnAnyEnemyDied += Refill;
    private void OnDisable() => BaseEnemy.OnAnyEnemyDied -= Refill;

    private void Start()
    {
        currentEnergy = stats.MaxEnergy;
    }

    /// <summary>Tries to spend energy. Returns true if the action is allowed.</summary>
    public bool UseEnergy(float usage)
    {
        // NOTE: energy consumption is currently bypassed (preserved from the original
        // implementation). Remove this early return when you want costs to apply.
        return true;

        if (!stats.useEnergy) return true;

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
        currentEnergy = stats.MaxEnergy;
    }

    /// <summary>Accumulate energy at the given per-second rate, clamped to MaxEnergy.</summary>
    public void Regen(float ratePerSecond)
    {
        currentEnergy = Mathf.Min(currentEnergy + ratePerSecond * Time.deltaTime, stats.MaxEnergy);
    }
}
