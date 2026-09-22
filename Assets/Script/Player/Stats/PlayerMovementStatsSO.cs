using UnityEngine;

[CreateAssetMenu(fileName = "New Player Movement Stats", menuName = "Swingscape/Player Stats/Movement")]
public class PlayerMovementStatsSO : ScriptableObject
{
    [Header("Movement")]
    public float walkSpeed = 8f;
    public float acceleration = 50f;
    public float deceleration = 40f;
    public float AirMaxSpeed = 20f;

    [Header("Jump")]
    public bool hasVariableJumpHeight = true;
    public float jumpPower = 5f;
    public float coyoteTime = 0.2f;
    public float jumpBuffferingTime = 0.2f;

    [Header("Gravity")]
    public float RealisticGravity = 30f;
    public bool hasFallingExtraGrav = true;
    public float extraGravityAmount = 3;

    [Header("Apex modifier")]
    public bool hasApexModifier = true;
    public float apexVertVelocityDetection = 0.7f;
    public float apexSpeedMult = 1.2f;
    public float apexFloatPower = 1f;

    [Header("Max fall speed")]
    public bool hasMaxFallSpeed = true;
    public float terminalVelocity = -10f;

    [Header("Crouch/Slide")]
    public bool enableCrouch = true;
    public bool holdToCrouch = true;
    public float crouchHeight = .75f;
    public float speedReduction = .5f;

    [Header("Floating Capsule")]
    public float rideHeight = 1.5f;
    public float rideSpringStrength = 50f;
    public float rideSpringDamper = 5f;

    [Header("HeadBob")]
    public bool enableHeadBob = true;
    public float bobSpeed = 10f;
    public Vector3 bobAmount = new Vector3(.15f, .05f, 0f);
}
