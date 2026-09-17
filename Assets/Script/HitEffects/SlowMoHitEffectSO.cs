using UnityEngine;

[CreateAssetMenu(fileName = "New Slow Mo Hit Effect", menuName = "Swingscape/Hit Effects/Slow Mo")]
public class SlowMoHitEffectSO : HitEffectSO
{
    [Tooltip("Sustained time scale applied while the effect is active")]
    [SerializeField] private float timeScale = 0.5f;
    [Tooltip("How long (real seconds) the slow-mo lasts before time returns to normal")]
    [SerializeField] private float duration = 0.2f;

    protected override void Apply(GameObject target)
    {
        TimeEffects.TriggerTimedSlowMo(timeScale, duration);
    }
}
