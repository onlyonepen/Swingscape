using UnityEngine;

/// <summary>
/// Owns grapple target selection: the aim-assisted ray/sphere cast that decides what
/// the player will grapple (terrain via the Swingable layer, enemies via the Grappleable
/// component) and the on-screen prediction reticle. Extracted from PlayerStateManager so
/// the state machine stays focused on state logic.
/// </summary>
public class GrappleTargeting : MonoBehaviour
{
    [Header("Reticle")]
    [Tooltip("UI element (child of the main HUD canvas) that acts as the lock-on indicator.")]
    public RectTransform predictionPoint;
    [Tooltip("The main HUD canvas predictionPoint lives on. Its render mode decides how the world point is projected.")]
    public Canvas targetCanvas;

    [Header("Range & target layers")]
    [Tooltip("Terrain/swing-point layer. Enemy targets are found by Grappleable component presence, not a layer.")]
    public LayerMask Swingable;

    [Header("Stats")]
    public PlayerGrappleStatsSO stats;

    private Camera cam;
    private PlayerStateManager stateManager;

    private void Awake()
    {
        PlayerManager playerManager = GetComponentInParent<PlayerManager>();
        cam = playerManager.Cam;
        stateManager = playerManager.Locomotion;
    }

    /// <summary>Hide the prediction reticle (states call this when grapple isn't active).</summary>
    public void HidePredictionPoint()
    {
        predictionPoint.gameObject.SetActive(false);
    }

    /// <summary>
    /// Aim-assisted grapple target selection. Returns the chosen hit (default RaycastHit
    /// if none) and updates the prediction reticle to match.
    /// </summary>
    public RaycastHit Predict()
    {
        if (!stateManager.canGrapple)
        {
            HidePredictionPoint();
            return new RaycastHit();
        }

        // Everything except the player: enemy targets are told apart from plain scenery by
        // Grappleable component presence, not a layer, so the query mask stays broad.
        LayerMask queryMask = ~GlobalReference.Instance.playerLayer;
        // Blocking check must use the same broad mask as the query: a nearer swingable point
        // or enemy is a valid line-of-sight blocker too, not just terrain, otherwise aim assist
        // can pick a farther target hidden behind a closer one.
        LayerMask obstacleMask = queryMask;

        // --- 1. DIRECT RAYCAST ---
        RaycastHit directHitEnemy = new RaycastHit();
        bool foundDirectEnemy = false;

        RaycastHit directHitSwing = new RaycastHit();
        bool foundDirectSwing = false;

        // Check perfectly down the center first
        if (Physics.Raycast(cam.transform.position, cam.transform.forward, out RaycastHit tempDirect, stats.GrappleMaxDistance, queryMask))
        {
            GameObject hitObj = tempDirect.collider.gameObject;
            bool isSwingable = ((1 << hitObj.layer) & Swingable) != 0;

            // Grappleable enemies take priority over terrain even on the direct raycast
            if (Grappleable.Resolve(hitObj) != GrappleType.Normal)
            {
                directHitEnemy = tempDirect;
                foundDirectEnemy = true;
            }
            else if (isSwingable)
            {
                directHitSwing = tempDirect;
                foundDirectSwing = true;
            }
        }

        // --- 2. AIM ASSIST (SPHERECAST) ---
        RaycastHit[] hits = Physics.SphereCastAll(
            cam.transform.position,
            stats.maxAimAssistRadius,
            cam.transform.forward,
            stats.GrappleMaxDistance,
            queryMask
        );

        RaycastHit bestAssistEnemyHit = new RaycastHit();
        float bestEnemyScore = -1f;
        bool foundAssistEnemy = false;

        RaycastHit bestAssistSwingHit = new RaycastHit();
        float bestSwingScore = -1f;
        bool foundAssistSwing = false;

        foreach (RaycastHit hit in hits)
        {
            // Unity Quirk: If the SphereCast starts inside a collider, hit.point returns Vector3.zero.
            // This line prevents mathematical errors when calculating the localHitPoint.
            if (hit.point == Vector3.zero) continue;

            Vector3 localHitPoint = hit.point - cam.transform.position;
            float distanceAlongRay = Vector3.Dot(localHitPoint, cam.transform.forward);

            if (distanceAlongRay < 0) continue;

            // Dynamic cone calculation
            float currentAllowedRadius = Mathf.Lerp(stats.minAimAssistRadius, stats.maxAimAssistRadius, distanceAlongRay / stats.GrappleMaxDistance);
            Vector3 pointOnCenterLine = cam.transform.position + (cam.transform.forward * distanceAlongRay);
            float distanceFromCenter = Vector3.Distance(pointOnCenterLine, hit.point);

            if (distanceFromCenter > currentAllowedRadius)
                continue;

            // FIXED: Check for blocking obstacles safely!
            // Output the hit data, and verify we aren't just hitting the object we want to grapple.
            if (Physics.Linecast(cam.transform.position, hit.point, out RaycastHit blockHit, obstacleMask))
            {
                if (blockHit.collider != hit.collider)
                {
                    continue; // It's blocked by a different obstacle
                }
            }

            GameObject candidate = hit.collider.gameObject;
            bool isSwingable = ((1 << candidate.layer) & Swingable) != 0;
            bool isEnemy = Grappleable.Resolve(candidate) != GrappleType.Normal;

            // Not on the swingable layer and no Grappleable component: not a valid grapple candidate at all
            if (!isSwingable && !isEnemy) continue;

            Vector3 directionToHit = localHitPoint.normalized;
            float alignmentScore = Vector3.Dot(cam.transform.forward, directionToHit);

            // Separate highest scoring enemy and highest scoring terrain
            if (isEnemy)
            {
                if (!foundAssistEnemy || alignmentScore > bestEnemyScore)
                {
                    bestAssistEnemyHit = hit;
                    bestEnemyScore = alignmentScore;
                    foundAssistEnemy = true;
                }
            }
            else
            {
                if (!foundAssistSwing || alignmentScore > bestSwingScore)
                {
                    bestAssistSwingHit = hit;
                    bestSwingScore = alignmentScore;
                    foundAssistSwing = true;
                }
            }
        }

        // --- 3. PRIORITY RESOLUTION ---
        RaycastHit finalHit = new RaycastHit();
        bool hasValidHit = false;

        // Grappleable enemies always outrank terrain: 1. direct enemy, 2. assisted enemy,
        // 3. direct terrain, 4. assisted terrain.
        if (foundDirectEnemy)
        {
            finalHit = directHitEnemy;
            hasValidHit = true;
        }
        else if (foundAssistEnemy)
        {
            finalHit = bestAssistEnemyHit;
            hasValidHit = true;
        }
        else if (foundDirectSwing)
        {
            finalHit = directHitSwing;
            hasValidHit = true;
        }
        else if (foundAssistSwing)
        {
            finalHit = bestAssistSwingHit;
            hasValidHit = true;
        }

        // --- 4. VISUAL FEEDBACK ---
        // Project the world hit point onto the HUD canvas so the indicator stays a fixed
        // screen size instead of scaling/skewing with distance like a world-space object would.
        if (hasValidHit)
        {
            Vector3 screenPoint = cam.WorldToScreenPoint(finalHit.point);
            if (screenPoint.z > 0) // in front of the camera
            {
                Camera canvasCam = targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : targetCanvas.worldCamera;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)targetCanvas.transform, screenPoint, canvasCam, out Vector2 localPoint))
                {
                    predictionPoint.gameObject.SetActive(true);
                    predictionPoint.anchoredPosition = localPoint;
                }
                else
                {
                    predictionPoint.gameObject.SetActive(false);
                }
            }
            else
            {
                predictionPoint.gameObject.SetActive(false);
            }
        }
        else
        {
            predictionPoint.gameObject.SetActive(false);
        }

        return finalHit;
    }
}
