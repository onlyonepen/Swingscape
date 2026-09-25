using UnityEngine;

public class PlayerBaseMovement : MonoBehaviour
{
    [HideInInspector] public Rigidbody rb;
    private IPlayerInput playerInput;

    #region seralize
    [Header("seralize")]
    public Collider _col;
    [SerializeField] private LayerMask GroundLayer;
    #endregion

    #region Stats
    [Header("Stats")]
    public PlayerMovementStatsSO stats;
    public PlayerCameraStatsSO cameraStats;
    #endregion

    #region Camera Movement Variables
    [Header("Camera control")]

    public Camera playerCamera;
    public bool cameraCanMove = true;
    public bool lockCursor = true;

    // Internal Variables
    private float yaw = 0.0f;
    private float pitch = 0.0f;

    #endregion

    #region Movement Variables
    [Header("Movement")]

    public bool playerCanMove = true;
    // Seeded from stats.walkSpeed in Awake — Crouch() mutates this at runtime, so it can't
    // read straight from the (shared) SO or crouching would permanently alter the asset.
    [HideInInspector] public float walkSpeed;

    // Internal Variables
    private bool isWalking = false;

    #region Jump
    [Header("Jump")]

    public bool enableJump = true;

    // Internal Variables
    private float jumpMuteTimer = 0f;
    private const float jumpMuteDuration = 0.2f; // How long to ignore the spring (seconds)
    [HideInInspector] public bool isGrounded = false;
    private float coyoteTimer = 0;
    private float bufferingTimer = 0;
    private bool isExtraGravOn = false;

    #endregion

    #region Extra gravity
    // Seeded from stats.hasFallingExtraGrav in Awake — grapple states toggle this at runtime
    // (GrappleLeapState/GrapplePullintoState), so it can't read straight from the shared SO.
    [HideInInspector] public bool hasFallingExtraGrav;

    #endregion

    #region Crouch
    // Internal Variables
    private bool isCrouched = false;
    private Vector3 originalScale;

    #endregion
    #endregion

    #region Floating Capsule

    [Header("Floating Capsule")]

    [HideInInspector] public bool FloatingCapsuleActive = true;

    #endregion

    #region Head Bob
    [Header("HeadBob")]
    public Transform joint;

    // Internal Variables
    private Vector3 jointOriginalPos;
    private float timer = 0;

    #endregion

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        playerInput = GetComponentInParent<PlayerManager>().Input;

        walkSpeed = stats.walkSpeed;
        hasFallingExtraGrav = stats.hasFallingExtraGrav;

        playerCamera.fieldOfView = cameraStats.fov;
        originalScale = transform.localScale;
        jointOriginalPos = joint.localPosition;
    }

    void Start()
    {
        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
        }

        Physics.gravity = Vector3.down * stats.RealisticGravity;
    }

    private void Update()
    {
        coyoteTimer -= Time.deltaTime;
        bufferingTimer -= Time.deltaTime;
        jumpMuteTimer -= Time.deltaTime;

        CheckGround();

        #region Camera

        // Control camera movement
        if (cameraCanMove)
        {
            yaw = transform.localEulerAngles.y + playerInput.Look.x * cameraStats.mouseSensitivity;

            if (!cameraStats.invertCamera)
            {
                pitch -= cameraStats.mouseSensitivity * playerInput.Look.y;
            }
            else
            {
                // Inverted Y
                pitch += cameraStats.mouseSensitivity * playerInput.Look.y;
            }

            // Clamp pitch between lookAngle
            pitch = Mathf.Clamp(pitch, -cameraStats.maxLookAngle, cameraStats.maxLookAngle);

            transform.localEulerAngles = new Vector3(0, yaw, 0);
            playerCamera.transform.localEulerAngles = new Vector3(pitch, 0, 0);
        }

        #endregion

        if (playerCanMove)
        {
            #region Jump

            if (isGrounded)
            {
                coyoteTimer = stats.coyoteTime;
                isExtraGravOn = false;
            }

            if (playerInput.JumpPressed)
            {
                bufferingTimer = stats.jumpBuffferingTime;
            }

            if (enableJump && bufferingTimer > 0f && coyoteTimer > 0f)
            {
                Jump();
                bufferingTimer = 0f;
                coyoteTimer = 0f;
            }

            #endregion

            #region Crouch

            if (stats.enableCrouch)
            {
                if (playerInput.CrouchPressed && !stats.holdToCrouch && isGrounded)
                {
                    Crouch();
                }

                if (playerInput.CrouchPressed && stats.holdToCrouch && isGrounded)
                {
                    isCrouched = false;
                    Crouch();
                }
                else if (playerInput.CrouchReleased && stats.holdToCrouch)
                {
                    isCrouched = true;
                    Crouch();
                }
            }

            #endregion

        }

        #region 0 Velocity snap
        Vector3 inputVelocity = new Vector3(playerInput.Move.x, 0, playerInput.Move.y);
        Vector3 nonVerticalVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
        if (inputVelocity.magnitude == 0 && nonVerticalVelocity.magnitude <= 1f)
        {
            rb.linearVelocity = Vector3.up * rb.linearVelocity.y;
        }

        #endregion


        if (stats.enableHeadBob)
        {
            Vector3 horizontalVel = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
            isWalking = isGrounded && horizontalVel.magnitude > 0.1f;
            HeadBob();
        }
    }

    void FixedUpdate()
    {
        //rb.AddForce(Vector3.down * (RealisticGravity - 9.8f),ForceMode.Acceleration);

        #region Movement

        if (playerCanMove)
        {
            Vector3 moveInput = new Vector3(playerInput.MoveRaw.x, 0, playerInput.MoveRaw.y);
            if (moveInput.magnitude > 1) moveInput.Normalize();

            if (isGrounded)
            {
                Vector3 targetVelocity = transform.TransformDirection(moveInput) * walkSpeed;

                Vector3 currentVelocity = rb.linearVelocity;
                currentVelocity.y = 0;

                float driveForce = moveInput.magnitude > 0 ? stats.acceleration : stats.deceleration;

                Vector3 newVelocity = Vector3.MoveTowards(currentVelocity, targetVelocity, driveForce * Time.fixedDeltaTime);

                Vector3 velocityChange = (newVelocity - currentVelocity);

                rb.AddForce(velocityChange, ForceMode.VelocityChange);
            }
            else
            {
                Vector3 airForce = transform.TransformDirection(moveInput) * stats.airAcceleration;
                rb.AddForce(airForce, ForceMode.Acceleration);

                if (rb.linearVelocity.magnitude > stats.AirMaxSpeed)
                {
                    Vector3 cappedVelocity = rb.linearVelocity.normalized * stats.AirMaxSpeed;
                    rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, cappedVelocity, stats.airMaxSpeedLerpSpeed * Time.fixedDeltaTime);
                }
            }
        }

        #endregion

        #region Apex modifier

        if (stats.hasApexModifier && rb.linearVelocity.y < stats.apexVertVelocityDetection)
        {
            rb.AddForce(Vector3.up * stats.apexFloatPower, ForceMode.Force);
            //AddSpeed
        }

        #endregion

        #region ExtraGrav

        bool variableJumpHeightActive = stats.hasVariableJumpHeight && !playerInput.JumpHeld;
        isExtraGravOn = hasFallingExtraGrav && (rb.linearVelocity.y < 0 || variableJumpHeightActive);
        if (isExtraGravOn)
        {
            rb.AddForce(Vector3.down * stats.extraGravityAmount, ForceMode.Acceleration);
        }

        #endregion

        #region Terminal velocity

        if(stats.hasMaxFallSpeed && rb.linearVelocity.y < stats.terminalVelocity)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, stats.terminalVelocity, rb.linearVelocity.z);
        }

        #endregion

        if (FloatingCapsuleActive)
        {
            floatingCapsule();
        }
    }

    private void CheckGround()
    {
        isGrounded = Physics.Raycast(transform.position, Vector3.down, stats.rideHeight + 0.1f, GroundLayer);
    }

    private void floatingCapsule()
    {
        if (jumpMuteTimer > 0) return;

        Ray ray = new Ray(transform.position, Vector3.down);
        if (Physics.Raycast(ray, out RaycastHit hit, stats.rideHeight + 0.1f, GroundLayer))
        {
            float distance = hit.distance;

            float rayDirVelocity = Vector3.Dot(Vector3.down, rb.linearVelocity);
            float relVel = rayDirVelocity;
            float xLen = distance - stats.rideHeight;
            float springForce = (xLen * stats.rideSpringStrength) - (relVel * stats.rideSpringDamper);

            Debug.DrawLine(transform.position, transform.position + (Vector3.down * (stats.rideHeight + 0.1f)), Color.red);
            rb.AddForce(Vector3.down * springForce);
        }
    }

    public void Jump()
    {
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
        rb.AddForce(0f, stats.jumpPower, 0f, ForceMode.Impulse);
        isGrounded = false;

        jumpMuteTimer = jumpMuteDuration;

        if (isCrouched && !stats.holdToCrouch)
        {
            Crouch();
        }
    }

    private void Crouch()
    {
        // Calculate how much the scale is changing
        // Note: If you are using a standard Unity Capsule (which is 2 units tall),
        // the bottom moves exactly by the difference in scale.
        // If your character model is 1 unit tall, you would divide this by 2f.
        float heightDifference = originalScale.y - stats.crouchHeight;

        if (isCrouched)
        {
            transform.localScale = new Vector3(originalScale.x, originalScale.y, originalScale.z);

            // Push the player up so the expanding collider doesn't clip into the floor
            transform.position += new Vector3(0, heightDifference, 0);

            walkSpeed /= stats.speedReduction;
            isCrouched = false;
        }
        else
        {
            transform.localScale = new Vector3(originalScale.x, stats.crouchHeight, originalScale.z);

            // Push the player down so they don't float momentarily after shrinking
            transform.position -= new Vector3(0, heightDifference, 0);

            walkSpeed *= stats.speedReduction;
            isCrouched = true;
        }
    }

    private void HeadBob()
    {
        if (isWalking)
        {
            // Calculates HeadBob speed during crouched movement
            if (isCrouched)
            {
                timer += Time.deltaTime * (stats.bobSpeed * stats.speedReduction);
            }
            else
            {
                timer += Time.deltaTime * stats.bobSpeed;
            }
            joint.localPosition = new Vector3(jointOriginalPos.x + Mathf.Sin(timer) * stats.bobAmount.x, jointOriginalPos.y + Mathf.Sin(timer) * stats.bobAmount.y, jointOriginalPos.z + Mathf.Sin(timer) * stats.bobAmount.z);
        }
        else
        {
            // Resets when play stops moving
            timer = 0;
            joint.localPosition = new Vector3(Mathf.Lerp(joint.localPosition.x, jointOriginalPos.x, Time.deltaTime * stats.bobSpeed), Mathf.Lerp(joint.localPosition.y, jointOriginalPos.y, Time.deltaTime * stats.bobSpeed), Mathf.Lerp(joint.localPosition.z, jointOriginalPos.z, Time.deltaTime * stats.bobSpeed));
        }
    }
}