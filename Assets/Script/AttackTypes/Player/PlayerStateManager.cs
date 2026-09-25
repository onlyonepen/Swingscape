using DG.Tweening;
using Script.Player.States;
using UnityEngine;
using UnityEngine.UI;
using VInspector;

public class PlayerStateManager : MonoBehaviour
{
    [ReadOnly] public string curreentState;

    [Header("BasicReference")]
    public PlayerManager Manager;
    /// <summary>Shortcut to the active input source so states can read manager.Input.X</summary>
    public IPlayerInput Input => Manager.Input;
    // These are populated from the hub in Awake (single source of truth) — no need to wire them here.
    [HideInInspector] public Rigidbody rb;
    [HideInInspector] public PlayerBaseMovement PBM;
    [HideInInspector] public Camera Cam;
    [HideInInspector] public PlayerCameraController camController;
    [HideInInspector] public PlayerHpManager playerHp;
    [HideInInspector] public PlayerEnergy Energy;
    [HideInInspector] public GrappleTargeting Targeting;
    [HideInInspector] public FootstepManager footstepManager;
    // State-machine-owned config (stays wired here).
    public PlayerRUD RUD = new PlayerRUD();
    public LayerMask TerrainLayer;
    public Transform feetTrans;
    [Header("Stats")]
    public PlayerCameraStatsSO cameraStats;
    public PlayerLocomotionStatsSO locomotionStats;
    public PlayerGrappleStatsSO grappleStats;
    private float currentFov;
    public Transform SideRotateJoint;
    [Header("Grapple")]
    public Transform grappleGun;
    public Transform Guntip;
    public Transform GrappleArm;
    internal Vector3 initialHandPos;
    internal Quaternion initialHandRot;
    public LineRenderer GrappleLr;

    [Header("Grapple Unlock")]
    [Tooltip("Whether the player has the grapple hook when the scene starts. Overridden at runtime by CheckpointManager/ObtainGrapple based on GameValue.ObtainedGrapple.")]
    public bool canGrapple = true;

    // Wall jump coyote time — refreshed every frame WallRunningState is actually touching a wall,
    // so a jump pressed shortly after leaving the wall (or the wall run ending) still fires.
    private float wallJumpCoyoteTimer;
    private Vector3 lastWallJumpNormal;
    public bool CanCoyoteWallJump => wallJumpCoyoteTimer > 0f;

    // Blocks re-entering wall run for a moment after a wall jump so the player doesn't
    // instantly reattach to the same wall.
    private float wallRunLockoutTimer;
    public bool CanWallRun => wallRunLockoutTimer <= 0f;

    [Header("DiedState")]
    public Image redScreenOverlay;
    public float deathDuration = 1f;
    public GameObject gameOverScreen;
    
    #region states

    public PlayerState CurrentState;

    public PlayerState BaseState = new PlayerBaseState();
    public PlayerState ThrowGrappleState = new ThrowGrappleState();
    public PlayerState pullRopeBackState = new PullBackRopeState();
    public PlayerState SwingState = new SwingState();
    public PlayerState GrapplePullState = new GrapplePullState();
    public PlayerState GrapplePullinState = new GrapplePullintoState();
    public PlayerState GrappleLeapState = new GrappleLeapState();
    public PlayerState WallRunState = new WallRunningState();
    public PlayerState MantleState = new MantleState();
    public PlayerState SlideState = new SlideState();
    public PlayerState DiedState = new DiedState();

    #endregion

    private void Awake()
    {
        // Pull every sibling from the hub — the single place references are wired.
        if (!Manager) Manager = GetComponentInParent<PlayerManager>();
        rb              = Manager.rb;
        PBM             = Manager.Movement;
        Cam             = Manager.Cam;
        camController    = Manager.CameraController;
        playerHp        = Manager.Health;
        Energy          = Manager.Energy;
        Targeting       = Manager.Targeting;
        footstepManager = Manager.Footsteps;
    }

    private void Start()
    {
        currentFov = cameraStats.minFov;
        initialHandPos = GrappleArm.localPosition;
        initialHandRot = GrappleArm.localRotation;

        CurrentState = BaseState;
        CurrentState.OnStateEnter(this);
    }

    private void OnEnable()
    {
        if (playerHp != null) playerHp.OnDied += HandleDied;
    }

    private void OnDisable()
    {
        if (playerHp != null) playerHp.OnDied -= HandleDied;
    }

    private void HandleDied()
    {
        ChangeState(DiedState);
    }

    void Update()
    {
        wallJumpCoyoteTimer -= Time.deltaTime;
        wallRunLockoutTimer -= Time.deltaTime;

        CurrentState.OnStateUpdate();
        EnergyRegen();
        UpdateFov();
    }

    void FixedUpdate()
    {
        CurrentState.OnStatePhysicsUpdate();
    }

    public void ChangeState(PlayerState state)
    {
        CurrentState.OnStateExit();
        CurrentState = state;
        CurrentState.OnStateEnter(this);
    }
    private void OnTriggerEnter(Collider other)
    {
        CurrentState.OnStateTriggerEnter(other);
    }
    
    public void GuntipPointToGrapple()
    {
        grappleGun.LookAt(RUD.GrapplePoint);
    }
    public void GuntipDefault()
    {
        grappleGun.localRotation = Quaternion.Euler(0f, 0f, 0f);
        //grappleGun.DOLocalRotate(Vector3.zero, .5f);
    }
    public void WaitToChangeState(PlayerState state, float WaitDur , float EnterTime)
    {
        if(Time.time - EnterTime > WaitDur)
        {
            ChangeState(state);
        }
    }
    public void RefreshWallJumpCoyote(Vector3 wallNormal)
    {
        wallJumpCoyoteTimer = locomotionStats.WallJumpCoyoteTime;
        lastWallJumpNormal = wallNormal;
    }

    public void ApplyWallJump(Vector3 wallNormal)
    {
        Vector3 jumpDir = (wallNormal.normalized * locomotionStats.WallJumpForce) + (transform.up.normalized * PBM.stats.jumpPower);
        rb.AddForce(jumpDir, ForceMode.Impulse);
        wallJumpCoyoteTimer = 0f;
        wallRunLockoutTimer = locomotionStats.WallJumpLockoutTime;
    }

    public void ApplyCoyoteWallJump()
    {
        ApplyWallJump(lastWallJumpNormal);
    }

    public Vector3 GroundNormal()
    {
        if(!PBM.isGrounded) return Vector3.zero;
        else
        {
            RaycastHit hit;
            Physics.Raycast(transform.position, Vector3.down, out hit, 5f, TerrainLayer);
            return hit.normal;
        }
    }

    private void EnergyRegen()
    {
        float rate = PBM.isGrounded ? Energy.stats.GroundedEnergyRegeneration : CurrentState.EnergyRegenRate;
        Energy.Regen(rate);
    }


    public void UpdateFov()
    {
        float currentSpeed = rb.linearVelocity.magnitude;
        float speedFactor = Mathf.InverseLerp(cameraStats.fovChangeTreshold, cameraStats.MaxSpeedForFovChange, currentSpeed);
        float logFactor = Mathf.Log10(1f + (speedFactor * 9f));
        float targetFov = Mathf.Lerp(cameraStats.minFov, cameraStats.maxFov, logFactor);
        currentFov = Mathf.Lerp(currentFov, targetFov, Time.deltaTime * cameraStats.fovSmoothSpeed);
        camController.changeFov(currentFov);
    }
}

public abstract class PlayerState
{
    public PlayerStateManager manager;
    public GameObject player;

    public float stateEnterTime;
    public virtual void OnStateEnter(PlayerStateManager gamestateManager)
    {
        manager = gamestateManager;
        manager.curreentState = this.ToString();
        player = manager.gameObject;
        stateEnterTime = Time.time;
    }
    public virtual void OnStateUpdate() { }
    public virtual void OnStatePhysicsUpdate() { }
    public virtual void OnStateExit() { }
    public virtual void OnStateTriggerEnter(Collider collider) { }

    /// <summary>Energy regen rate while airborne in this state. Grounded always overrides to GroundedEnergyRegeneration.</summary>
    public virtual float EnergyRegenRate => manager.Energy.stats.EnergyRegeneration;
}

public class PlayerRUD
{
    [HideInInspector] public Vector3 GrapplePoint;
    [HideInInspector] public GameObject GrappledObject;
    [HideInInspector] public Vector3 MantlePoint;
}
