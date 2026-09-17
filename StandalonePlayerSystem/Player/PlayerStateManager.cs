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
    [Header("CameraFov")]
    public float minFov = 80f;
    public float maxFov = 100f;
    public float fovSmoothSpeed = 10f;
    public float fovChangeTreshold = 10f;
    public float MaxSpeedForFovChange = 60f;
    private float currentFov;
    [Header("Wall run")]
    public float WallRunAccel = 50f;
    public float WallRunMaxSpeed = 12f; 
    public float WallClimbSpeed = 3f;
    public float WallJumpForce = 10;
    public float WallCheckDistance = 1f;
    public float GroundCheckDistance = 2f;
    public Transform SideRotateJoint;
    [Header("Sliding")]
    public float SlideSpeedMult = 0.2f;
    public float SlideSpeedTreshold = 2f;
    public float SlideFriction = 0.8f;
    [Header("Mantle")]
    public float PlayerHeightOffset = 1.6f;
    public float MantleFrontCastDist = 1.2f;
    [Header("Grapple")]
    public Transform grappleGun;
    public Transform Guntip;
    public Transform GrappleArm;
    public float GrappleEnemyOffset = 1.5f;
    internal Vector3 initialHandPos;
    internal Quaternion initialHandRot;
    public float GrappleTravelTime;
    public LineRenderer GrappleLr;
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

    [HideInInspector] public bool canGrapple;

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
        currentFov = minFov;
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
        float rate = PBM.isGrounded ? Energy.GroundedEnergyRegeneration : CurrentState.EnergyRegenRate;
        Energy.Regen(rate);
    }


    public void UpdateFov()
    {
        float currentSpeed = rb.linearVelocity.magnitude;
        float speedFactor = Mathf.InverseLerp(fovChangeTreshold, MaxSpeedForFovChange, currentSpeed);
        float logFactor = Mathf.Log10(1f + (speedFactor * 9f));
        float targetFov = Mathf.Lerp(minFov, maxFov, logFactor);
        currentFov = Mathf.Lerp(currentFov, targetFov, Time.deltaTime * fovSmoothSpeed);
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
    public virtual float EnergyRegenRate => manager.Energy.EnergyRegeneration;
}

public class PlayerRUD
{
    [HideInInspector] public Vector3 GrapplePoint;
    [HideInInspector] public GameObject GrappledObject;
    [HideInInspector] public Vector3 MantlePoint;
}
