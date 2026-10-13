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
    [SerializeField] private KeyCode pauseKey = KeyCode.Escape;
    [SerializeField] private KeyCode outOfBoundsRespawnKey = KeyCode.B;

    private const int AttackMouseButton = 0;   // left
    private const int GrappleMouseButton = 1;   // right

    // Single choke point for freezing player input while paused/menu'd — everything
    // below (except Pause/Respawn, which must work regardless of state) reads through this.
    private bool InputLocked => GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing;

    // --- Axes ---
    public Vector2 Move => InputLocked ? Vector2.zero : new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
    public Vector2 MoveRaw => InputLocked ? Vector2.zero : new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
    public Vector2 Look => InputLocked ? Vector2.zero : new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));

    // --- Jump ---
    public bool JumpPressed => !InputLocked && Input.GetKeyDown(jumpKey);
    public bool JumpHeld => !InputLocked && Input.GetKey(jumpKey);

    // --- Crouch / slide ---
    public bool CrouchPressed => !InputLocked && Input.GetKeyDown(crouchKey);
    public bool CrouchReleased => !InputLocked && Input.GetKeyUp(crouchKey);
    public bool CrouchHeld => !InputLocked && Input.GetKey(crouchKey);

    // --- Combat ---
    public bool AttackPressed => !InputLocked && Input.GetMouseButtonDown(AttackMouseButton);

    // --- Grapple ---
    public bool GrapplePressed => !InputLocked && Input.GetMouseButtonDown(GrappleMouseButton);
    public bool GrappleHeld => !InputLocked && Input.GetMouseButton(GrappleMouseButton);

    // --- Sprint / grapple-leap ---
    public bool SprintPressed => !InputLocked && Input.GetKeyDown(sprintKey);
    public bool SprintHeld => !InputLocked && Input.GetKey(sprintKey);

    // --- Discrete movement keys ---
    public bool ForwardHeld => !InputLocked && Input.GetKey(forwardKey);
    public bool ForwardReleased => !InputLocked && Input.GetKeyUp(forwardKey);
    public bool StrafeLeftHeld => !InputLocked && Input.GetKey(strafeLeftKey);
    public bool StrafeRightHeld => !InputLocked && Input.GetKey(strafeRightKey);

    // --- Meta (always live — must work regardless of game state) ---
    public bool RespawnPressed => Input.GetKeyDown(respawnKey);
    public bool PausePressed => Input.GetKeyDown(pauseKey);
    public bool OutOfBoundsRespawnPressed => Input.GetKeyDown(outOfBoundsRespawnKey);
}
