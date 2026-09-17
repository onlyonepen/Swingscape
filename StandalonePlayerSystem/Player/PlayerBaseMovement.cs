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

    #region Camera Movement Variables
    [Header("Camera control")]

    public Camera playerCamera;
    public float fov = 60f;
    public bool invertCamera = false;
    public bool cameraCanMove = true;
    public float mouseSensitivity = 2f;
    public float maxLookAngle = 50f;
    public bool lockCursor = true;

    // Internal Variables
    private float yaw = 0.0f;
    private float pitch = 0.0f;

    #endregion

    #region Movement Variables
    [Header("Movement")]

    public bool playerCanMove = true;
    public float walkSpeed = 8f;
    [SerializeField] private float acceleration = 50f; 
    [SerializeField] private float deceleration = 40f;
    [SerializeField] private float AirMaxSpeed = 20f;  

    // Internal Variables
    private bool isWalking = false;

    #region Jump
    [Header("Jump")]

    public bool enableJump = true;
    public bool hasVariableJumpHeight = true;
    public float jumpPower = 5f;
    public float coyoteTime = 0.2f;
    public float jumpBuffferingTime = 0.2f;

    // Internal Variables
    private float jumpMuteTimer = 0f;
    private const float jumpMuteDuration = 0.2f; // How long to ignore the spring (seconds)
    [HideInInspector] public bool isGrounded = false;
    private float coyoteTimer = 0;
    private float bufferingTimer = 0;
    private bool isExtraGravOn = false;

    #endregion

    #region Extra gravity
    [Header("Gravity")]

    public float RealisticGravity = 30f;
    public bool hasFallingExtraGrav = true;
    public float extraGravityAmount = 3;

    #endregion

    #region Apex modifier
    [Header("Apex modifier")]

    public bool hasApexModifier = true;
    public float apexVertVelocityDetection = 0.7f;
    public float apexSpeedMult = 1.2f;
    public float apexFloatPower = 1f;

    #endregion

    #region MaxFallSpeed
    [Header("Max fall speed")]

    public bool hasMaxFallSpeed = true;
    public float terminalVelocity = -10f;

    #endregion

    #region Crouch
    [Header("Crouch/Slide")]
    public bool enableCrouch = true;
    public bool holdToCrouch = true;
    public float crouchHeight = .75f;
    public float speedReduction = .5f;

    // Internal Variables
    private bool isCrouched = false;
    private Vector3 originalScale;

    #endregion
    #endregion

    #region Floating Capsule

    [Header("Floating Capsule")]

    [HideInInspector] public bool FloatingCapsuleActive = true;

    public float rideHeight = 1.5f; // Desired height above ground
    [SerializeField] private float rideSpringStrength = 50f; // How "stiff" the hover is
    [SerializeField] private float rideSpringDamper = 5f;

    #endregion

    #region Head Bob
    [Header("HeadBob")]
    public bool enableHeadBob = true;
    public Transform joint;
    public float bobSpeed = 10f;
    public Vector3 bobAmount = new Vector3(.15f, .05f, 0f);

    // Internal Variables
    private Vector3 jointOriginalPos;
    private float timer = 0;

    #endregion

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        playerInput = GetComponentInParent<PlayerManager>().Input;

        playerCamera.fieldOfView = fov;
        originalScale = transform.localScale;
        jointOriginalPos = joint.localPosition;
    }

    void Start()
    {
        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
        }

        Physics.gravity = Vector3.down * RealisticGravity;
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
            yaw = transform.localEulerAngles.y + playerInput.Look.x * mouseSensitivity;

            if (!invertCamera)
            {
                pitch -= mouseSensitivity * playerInput.Look.y;
            }
            else
            {
                // Inverted Y
                pitch += mouseSensitivity * playerInput.Look.y;
            }

            // Clamp pitch between lookAngle
            pitch = Mathf.Clamp(pitch, -maxLookAngle, maxLookAngle);

            transform.localEulerAngles = new Vector3(0, yaw, 0);
            playerCamera.transform.localEulerAngles = new Vector3(pitch, 0, 0);
        }

        #endregion

        if (playerCanMove)
        {
            #region Jump

            if (isGrounded)
            {
                coyoteTimer = coyoteTime;
                isExtraGravOn = false;
            }

            if (playerInput.JumpPressed)
            {
                bufferingTimer = jumpBuffferingTime;
            }

            if (enableJump && bufferingTimer > 0f && coyoteTimer > 0f)
            {
                Jump();
                bufferingTimer = 0f;
                coyoteTimer = 0f;
            }

            #endregion

            #region Crouch

            if (enableCrouch)
            {
                if (playerInput.CrouchPressed && !holdToCrouch)
                {
                    Crouch();
                }

                if (playerInput.CrouchPressed && holdToCrouch)
                {
                    isCrouched = false;
                    Crouch();
                }
                else if (playerInput.CrouchReleased && holdToCrouch)
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


        if (enableHeadBob)
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
            if (isGrounded)
            {
                if (moveInput.magnitude > 1) moveInput.Normalize();

                Vector3 targetVelocity = transform.TransformDirection(moveInput) * walkSpeed;

                Vector3 currentVelocity = rb.linearVelocity;
                currentVelocity.y = 0;

                float driveForce = moveInput.magnitude > 0 ? acceleration : deceleration;

                Vector3 newVelocity = Vector3.MoveTowards(currentVelocity, targetVelocity, driveForce * Time.fixedDeltaTime);

                Vector3 velocityChange = (newVelocity - currentVelocity);

                rb.AddForce(velocityChange, ForceMode.VelocityChange);
            }
            else
            {
                if(rb.linearVelocity.magnitude > AirMaxSpeed)
                {
                    rb.linearVelocity = rb.linearVelocity.normalized * AirMaxSpeed;
                }
            }
        }

        #endregion

        #region Apex modifier

        if (hasApexModifier && rb.linearVelocity.y < apexVertVelocityDetection)
        {
            rb.AddForce(Vector3.up * apexFloatPower, ForceMode.Force);
            //AddSpeed
        }

        #endregion

        #region ExtraGrav

        bool variableJumpHeightActive = hasVariableJumpHeight && !playerInput.JumpHeld;
        isExtraGravOn = hasFallingExtraGrav && (rb.linearVelocity.y < 0 || variableJumpHeightActive);
        if (isExtraGravOn)
        {
            rb.AddForce(Vector3.down * extraGravityAmount, ForceMode.Acceleration);
        }

        #endregion

        #region Terminal velocity

        if(hasMaxFallSpeed && rb.linearVelocity.y < terminalVelocity)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, terminalVelocity, rb.linearVelocity.z);
        }

        #endregion

        if (FloatingCapsuleActive)
        {
            floatingCapsule();
        }
    }

    private void CheckGround()
    {
        isGrounded = Physics.Raycast(transform.position, Vector3.down, rideHeight + 0.1f, GroundLayer);
    }

    private void floatingCapsule()
    {
        if (jumpMuteTimer > 0) return;

        Ray ray = new Ray(transform.position, Vector3.down);
        if (Physics.Raycast(ray, out RaycastHit hit, rideHeight + 0.1f, GroundLayer))
        {
            float distance = hit.distance;

            float rayDirVelocity = Vector3.Dot(Vector3.down, rb.linearVelocity);
            float relVel = rayDirVelocity;
            float xLen = distance - rideHeight;
            float springForce = (xLen * rideSpringStrength) - (relVel * rideSpringDamper);

            Debug.DrawLine(transform.position, transform.position + (Vector3.down * (rideHeight + 0.1f)), Color.red);
            rb.AddForce(Vector3.down * springForce);
        }
    }

    public void Jump()
    {
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
        rb.AddForce(0f, jumpPower, 0f, ForceMode.Impulse);
        isGrounded = false;

        jumpMuteTimer = jumpMuteDuration;

        if (isCrouched && !holdToCrouch)
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
        float heightDifference = originalScale.y - crouchHeight; 

        if (isCrouched)
        {
            transform.localScale = new Vector3(originalScale.x, originalScale.y, originalScale.z);
        
            // Push the player up so the expanding collider doesn't clip into the floor
            transform.position += new Vector3(0, heightDifference, 0);
        
            walkSpeed /= speedReduction;
            isCrouched = false;
        }
        else
        {
            transform.localScale = new Vector3(originalScale.x, crouchHeight, originalScale.z);
        
            // Push the player down so they don't float momentarily after shrinking
            transform.position -= new Vector3(0, heightDifference, 0);
        
            walkSpeed *= speedReduction;
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
                timer += Time.deltaTime * (bobSpeed * speedReduction);
            }
            else
            {
                timer += Time.deltaTime * bobSpeed;
            }
            joint.localPosition = new Vector3(jointOriginalPos.x + Mathf.Sin(timer) * bobAmount.x, jointOriginalPos.y + Mathf.Sin(timer) * bobAmount.y, jointOriginalPos.z + Mathf.Sin(timer) * bobAmount.z);
        }
        else
        {
            // Resets when play stops moving
            timer = 0;
            joint.localPosition = new Vector3(Mathf.Lerp(joint.localPosition.x, jointOriginalPos.x, Time.deltaTime * bobSpeed), Mathf.Lerp(joint.localPosition.y, jointOriginalPos.y, Time.deltaTime * bobSpeed), Mathf.Lerp(joint.localPosition.z, jointOriginalPos.z, Time.deltaTime * bobSpeed));
        }
    }
}