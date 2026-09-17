using UnityEngine;

/// <summary>
/// Central hub and single source of truth for the player's sub-system references.
/// External code (enemies, UI, level scripts) talks to the player through here via
/// GlobalReference.player. Internally, sub-components pull their siblings from this
/// hub in Awake, so references are wired in exactly one place.
///
/// Runs before other player components (execution order) so its references are
/// populated by the time siblings read them in their own Awake.
/// </summary>
[DefaultExecutionOrder(-50)]
public class PlayerManager : MonoBehaviour
{
    [Header("Sub-systems")]
    public PlayerStateManager Locomotion;   // traversal / movement finite state machine
    public PlayerHpManager Health;
    public PlayerEnergy Energy;
    public GrappleTargeting Targeting;
    public PlayerBaseMovement Movement;
    public PlayerAttacking Combat;           // separate, concurrent combat state machine
    public PlayerCameraController CameraController;
    public FootstepManager Footsteps;

    [Header("Shared refs")]
    public Rigidbody rb;
    public Camera Cam;

    /// <summary>Active input source. Resolved by interface, so swapping the legacy
    /// PlayerInput for a new-Input-System implementation needs no changes here.</summary>
    public IPlayerInput Input { get; private set; }

    private void Awake()
    {
        Input = GetComponentInChildren<IPlayerInput>();

        // The hub finds every sub-system itself. Inspector overrides win (the null checks),
        // otherwise each is resolved from this GameObject / its children.
        if (!Locomotion)       Locomotion       = GetComponentInChildren<PlayerStateManager>();
        if (!Health)           Health           = GetComponentInChildren<PlayerHpManager>();
        if (!Energy)           Energy           = GetComponentInChildren<PlayerEnergy>();
        if (!Targeting)        Targeting        = GetComponentInChildren<GrappleTargeting>();
        if (!Movement)         Movement         = GetComponentInChildren<PlayerBaseMovement>();
        if (!Combat)           Combat           = GetComponentInChildren<PlayerAttacking>();
        if (!CameraController) CameraController  = GetComponentInChildren<PlayerCameraController>();
        if (!Footsteps)        Footsteps        = GetComponentInChildren<FootstepManager>();

        if (!rb)  rb  = GetComponent<Rigidbody>();
        // Prefer the camera the movement script already points at over a blind child search.
        if (!Cam) Cam = Movement ? Movement.playerCamera : GetComponentInChildren<Camera>();
    }
}
