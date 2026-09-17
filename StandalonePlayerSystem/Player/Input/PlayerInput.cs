using UnityEngine;

/// <summary>
/// Legacy-Input-Manager implementation of <see cref="IPlayerInput"/>.
///
/// Every UnityEngine.Input call in the player system funnels through here, so key
/// bindings live in one place and the whole game can be re-bound or ported to the
/// new Input System by replacing this one component (see IPlayerInput).
///
/// Getters query live (same per-frame semantics as the original scattered reads),
/// so migrating a consumer from Input.X to Manager.Input.X is behaviour-preserving.
/// </summary>
public class PlayerInput : MonoBehaviour, IPlayerInput
{
    [Header("Bindings")]
    [SerializeField] private KeyCode jumpKey = KeyCode.Space;
    [SerializeField] private KeyCode crouchKey = KeyCode.LeftControl;
    [SerializeField] private KeyCode sprintKey = KeyCode.LeftShift;
    [SerializeField] private KeyCode forwardKey = KeyCode.W;
    [SerializeField] private KeyCode strafeLeftKey = KeyCode.A;
    [SerializeField] private KeyCode strafeRightKey = KeyCode.D;
    [SerializeField] private KeyCode respawnKey = KeyCode.R;

    private const int AttackMouseButton = 0;   // left
    private const int GrappleMouseButton = 1;   // right

    // --- Axes ---
    public Vector2 Move => new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
    public Vector2 MoveRaw => new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
    public Vector2 Look => new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));

    // --- Jump ---
    public bool JumpPressed => Input.GetKeyDown(jumpKey);
    public bool JumpHeld => Input.GetKey(jumpKey);

    // --- Crouch / slide ---
    public bool CrouchPressed => Input.GetKeyDown(crouchKey);
    public bool CrouchReleased => Input.GetKeyUp(crouchKey);
    public bool CrouchHeld => Input.GetKey(crouchKey);

    // --- Combat ---
    public bool AttackPressed => Input.GetMouseButtonDown(AttackMouseButton);

    // --- Grapple ---
    public bool GrapplePressed => Input.GetMouseButtonDown(GrappleMouseButton);
    public bool GrappleHeld => Input.GetMouseButton(GrappleMouseButton);

    // --- Sprint / grapple-leap ---
    public bool SprintPressed => Input.GetKeyDown(sprintKey);
    public bool SprintHeld => Input.GetKey(sprintKey);

    // --- Discrete movement keys ---
    public bool ForwardHeld => Input.GetKey(forwardKey);
    public bool ForwardReleased => Input.GetKeyUp(forwardKey);
    public bool StrafeLeftHeld => Input.GetKey(strafeLeftKey);
    public bool StrafeRightHeld => Input.GetKey(strafeRightKey);

    // --- Meta ---
    public bool RespawnPressed => Input.GetKeyDown(respawnKey);
}
