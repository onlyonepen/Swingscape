using UnityEngine;

[CreateAssetMenu(fileName = "New Player Energy Stats", menuName = "Swingscape/Player Stats/Energy")]
public class PlayerEnergyStatsSO : ScriptableObject
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
}
