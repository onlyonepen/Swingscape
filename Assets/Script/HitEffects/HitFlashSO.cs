using System.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(fileName = "New Hit flash Effect", menuName = "Swingscape/Hit Effects/Hit Flash")]
public class HitFlashSO : HitEffectSO
{
    [SerializeField] private float FlashDur = 0.1f;
    [SerializeField] private Material flashMaterial;

    protected override void Apply(GameObject target)
    {
        FlashRoutine(target);
    }

    private async Task FlashRoutine(GameObject target)
    {
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

        float elapsed = 0f;
        while (elapsed < FlashDur)
        {
            elapsed += Time.unscaledDeltaTime;
            await Task.Yield();
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].materials = originalMaterials[i];
        }
    }
}
