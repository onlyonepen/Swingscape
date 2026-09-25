using UnityEngine;

[CreateAssetMenu(fileName = "New Player Grapple Stats", menuName = "Swingscape/Player Stats/Grapple")]
public class PlayerGrappleStatsSO : ScriptableObject
{
    [Header("Targeting")]
    public float GrappleMaxDistance;
    public float minAimAssistRadius = 0.8f;
    public float maxAimAssistRadius = 5.0f;

    [Header("Grapple")]
    public float GrappleEnemyOffset = 1.5f;
    public float GrappleTravelTime;

    [Header("Swinging")]
    public float JointSpring = 4.5f;
    public float JointDamper = 7f;
    public float JointMassScale = 4.5f;
    public float AirControlFwdForce = 600;
    public float AirControlHorizontalForce = 400;
    public float SwingDashPower = 20;
    public float SwingDashMaxPower = 18;
    public float SwingDashMinPower = 5;

    [Header("Pull into")]
    public float PullIntoSpeed = 40f;
    public float OvershootYAxis = 3f;
}
