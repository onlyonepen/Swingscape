using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(fileName = "New Hit flash Effect", menuName = "Swingscape/Hit Effects/Hit Flash")]
public class HitFlashSO : HitEffectSO
{
    [SerializeField] private float FlashDur = 0.1f;
    [SerializeField] private Material flashMaterial;

    private class FlashState
    {
        public Renderer[] renderers;
        public Material[][] originalMaterials;
        public float endTime;
    }

    private readonly Dictionary<GameObject, FlashState> activeFlashes = new Dictionary<GameObject, FlashState>();

    protected override void Apply(GameObject target)
    {
        if (activeFlashes.TryGetValue(target, out FlashState existing))
        {
            existing.endTime = Time.unscaledTime + FlashDur;
            return;
        }

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
        Material[][] originalMaterials = new Material[renderers.Length][];

        for (int i = 0; i < renderers.Length; i++)
        {
            originalMaterials[i] = renderers[i].materials;
            Material[] flashMaterials = new Material[originalMaterials[i].Length];
            for (int j = 0; j < flashMaterials.Length; j++)
                flashMaterials[j] = flashMaterial;
            renderers[i].materials = flashMaterials;
        }

        FlashState state = new FlashState
        {
            renderers = renderers,
            originalMaterials = originalMaterials,
            endTime = Time.unscaledTime + FlashDur
        };
        activeFlashes[target] = state;

        FlashRoutine(target, state);
    }

    private async Task FlashRoutine(GameObject target, FlashState state)
    {
        while (Time.unscaledTime < state.endTime)
        {
            await Task.Yield();
        }

        for (int i = 0; i < state.renderers.Length; i++)
        {
            if (state.renderers[i] != null)
                state.renderers[i].materials = state.originalMaterials[i];
        }

        activeFlashes.Remove(target);
    }
}
