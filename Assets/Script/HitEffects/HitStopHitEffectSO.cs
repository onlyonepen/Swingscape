using UnityEngine;

[CreateAssetMenu(fileName = "New Hit Stop Hit Effect", menuName = "Swingscape/Hit Effects/Hit Stop")]
public class HitStopHitEffectSO : HitEffectSO
{
    [Tooltip("How long (real seconds) the freeze-frame lasts")]
    [SerializeField] private float duration = 0.05f;
    [Tooltip("If true, only the target freezes; if false, Time.timeScale freezes globally")]
    [SerializeField] private bool targetedOnly = false;

    protected override void Apply(GameObject target)
    {
        if (targetedOnly) TimeEffects.TriggerTargetedHitStop(target, duration);
        else TimeEffects.TriggerGlobalHitStop(duration);
    }
}
