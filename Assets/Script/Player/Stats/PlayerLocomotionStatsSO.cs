using UnityEngine;

[CreateAssetMenu(fileName = "New Player Locomotion Stats", menuName = "Swingscape/Player Stats/Locomotion")]
public class PlayerLocomotionStatsSO : ScriptableObject
{
    [Header("Wall run")]
    public float WallRunAccel = 50f;
    public float WallRunMaxSpeed = 12f;
    public float WallRunOverspeedDampRate = 10f;
    public float WallJumpForce = 10;
    public float WallCheckDistance = 1f;
    public float GroundCheckDistance = 2f;

    [Header("Sliding")]
    public float SlideSpeedMult = 0.2f;
    public float SlideSpeedTreshold = 2f;
    public float SlideFriction = 0.8f;

    [Header("Mantle")]
    public float PlayerHeightOffset = 1.6f;
    public float MantleFrontCastDist = 1.2f;
}
