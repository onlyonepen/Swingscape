using System.Threading.Tasks;
using UnityEngine;

/// <summary>Base class for a pluggable hit-reaction effect (slow-mo, hit-stop, etc.).
/// Concrete effects live as assets so an Attackable can mix-and-match several at once.</summary>
public abstract class HitEffectSO : ScriptableObject
{
    [Tooltip("Real-time seconds to wait after the hit before this effect applies. 0 = instant.")]
    [SerializeField] private float delay = 0f;

    /// <summary>Called by Attackable.TriggerHitEffects(). Handles the configured delay itself
    /// (no dependency on any MonoBehaviour runner), then hands off to Apply() —
    /// override Apply() in subclasses, not this method.</summary>
    public void Trigger(GameObject target)
    {
        if (delay <= 0f)
        {
            Apply(target);
            return;
        }
        _ = TriggerDelayed(target);
    }

    private async Task TriggerDelayed(GameObject target)
    {
        float elapsed = 0f;
        while (elapsed < delay)
        {
            elapsed += Time.unscaledDeltaTime;
            await Task.Yield();
        }
        Apply(target);
    }

    protected abstract void Apply(GameObject target);
}
