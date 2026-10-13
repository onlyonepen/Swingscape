using UnityEngine;

/// <summary>
/// Out-of-bounds safety net. Tracks the last standable ground position (feet
/// raycast, checked once a second, standable up to a 30 degree slope), and after
/// a sustained second with no ground layer anywhere nearby, prompts the player to
/// manually respawn there.
///
/// Deliberately manual, not auto-teleport: swinging between sky islands legitimately
/// spends a second or so with nothing underneath, so an instant trigger would yank
/// the player out of a normal swing.
/// </summary>
public class OutOfBoundsWatcher : MonoBehaviour
{
    public PlayerManager Manager;

    [Header("Safe position tracking")]
    [SerializeField] private float safeCheckInterval = 1f;
    [SerializeField] private float groundRaycastDistance = 5f;
    [SerializeField] private float maxStandableAngle = 30f;

    [Header("Out-of-bounds detection")]
    [SerializeField] private float overlapRadius = 3f;
    [SerializeField] private float sustainedNoGroundDuration = 1f;

    private Vector3 lastSafePosition;
    private Quaternion lastSafeRotation = Quaternion.identity;
    private bool hasSafePosition;

    private float safeCheckTimer;
    private float noGroundTimer;
    private bool promptShown;

    private void Awake()
    {
        if (!Manager) Manager = GetComponentInParent<PlayerManager>();
    }

    private void Start()
    {
        lastSafePosition = transform.position;
        lastSafeRotation = transform.rotation;
        hasSafePosition = true;
    }

    private void Update()
    {
        TrackSafePosition();
        TrackOutOfBounds();
    }

    private void TrackSafePosition()
    {
        safeCheckTimer -= Time.deltaTime;
        if (safeCheckTimer > 0f) return;
        safeCheckTimer = safeCheckInterval;

        Vector3 feetPos = FeetPosition();
        if (!Physics.Raycast(feetPos, Vector3.down, out RaycastHit hit, groundRaycastDistance, Manager.Locomotion.TerrainLayer))
            return;

        if (Vector3.Angle(hit.normal, Vector3.up) <= maxStandableAngle)
        {
            lastSafePosition = transform.position;
            lastSafeRotation = transform.rotation;
            hasSafePosition = true;
        }
    }

    private void TrackOutOfBounds()
    {
        bool hasGroundNearby = Physics.CheckSphere(FeetPosition(), overlapRadius, Manager.Locomotion.TerrainLayer);

        if (hasGroundNearby)
        {
            noGroundTimer = 0f;
            HidePrompt();
            return;
        }

        noGroundTimer += Time.deltaTime;
        if (noGroundTimer < sustainedNoGroundDuration) return;

        ShowPrompt();

        if (hasSafePosition && Manager.Input.OutOfBoundsRespawnPressed)
        {
            Manager.Locomotion.TeleportTo(lastSafePosition, lastSafeRotation);
            noGroundTimer = 0f;
            HidePrompt();
        }
    }

    private Vector3 FeetPosition()
    {
        Transform feet = Manager.Locomotion.feetTrans;
        return feet ? feet.position : transform.position;
    }

    private void ShowPrompt()
    {
        if (promptShown) return;
        promptShown = true;
        if (GameUIManager.Instance) GameUIManager.Instance.ShowOutOfBoundsPrompt(true);
    }

    private void HidePrompt()
    {
        if (!promptShown) return;
        promptShown = false;
        if (GameUIManager.Instance) GameUIManager.Instance.ShowOutOfBoundsPrompt(false);
    }
}
