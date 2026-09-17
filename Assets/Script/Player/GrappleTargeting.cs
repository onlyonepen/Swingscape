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
    public float GrappleMaxDistance;
    [Tooltip("Terrain/swing-point layer. Enemy targets are found by Grappleable component presence, not a layer.")]
    public LayerMask Swingable;

    [Header("Aim assist")]
    public float minAimAssistRadius = 0.8f;
    public float maxAimAssistRadius = 5.0f;

    private Camera cam;

    private void Awake()
    {
        cam = GetComponentInParent<PlayerManager>().Cam;
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
        LayerMask obstacleMask = GlobalReference.Instance.TerrainLayer;
        // Everything except the player: enemy targets are told apart from plain scenery by
        // Grappleable component presence, not a layer, so the query mask stays broad.
        LayerMask queryMask = ~GlobalReference.Instance.playerLayer;

        // --- 1. DIRECT RAYCAST ---
        RaycastHit directHitEnemy = new RaycastHit();
        bool foundDirectEnemy = false;

        RaycastHit directHitSwing = new RaycastHit();
        bool foundDirectSwing = false;

        // Check perfectly down the center first
        if (Physics.Raycast(cam.transform.position, cam.transform.forward, out RaycastHit tempDirect, GrappleMaxDistance, queryMask))
        {
            GameObject hitObj = tempDirect.collider.gameObject;
            bool isSwingable = ((1 << hitObj.layer) & Swingable) != 0;

            // SWAPPED: Check if the direct hit is terrain/swingable FIRST
            if (isSwingable)
            {
                directHitSwing = tempDirect;
                foundDirectSwing = true;
            }
            // Then check if the direct hit is a Grappleable enemy
            else if (Grappleable.Resolve(hitObj) != GrappleType.Normal)
            {
                directHitEnemy = tempDirect;
                foundDirectEnemy = true;
            }
        }

        // --- 2. AIM ASSIST (SPHERECAST) ---
        RaycastHit[] hits = Physics.SphereCastAll(
            cam.transform.position,
            maxAimAssistRadius,
            cam.transform.forward,
            GrappleMaxDistance,
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
            float currentAllowedRadius = Mathf.Lerp(minAimAssistRadius, maxAimAssistRadius, distanceAlongRay / GrappleMaxDistance);
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
            bool isEnemy = !isSwingable && Grappleable.Resolve(candidate) != GrappleType.Normal;

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

        // SWAPPED: 1. Terrain in Direct Raycast (Intentional Traversal)
        if (foundDirectSwing)
        {
            finalHit = directHitSwing;
            hasValidHit = true;
        }
        // SWAPPED: 2. Enemy in Direct Raycast (Intentional Combat)
        else if (foundDirectEnemy)
        {
            finalHit = directHitEnemy;
            hasValidHit = true;
        }
        // 3. Enemy in Aim Assist (Forgiving Combat)
        else if (foundAssistEnemy)
        {
            finalHit = bestAssistEnemyHit;
            hasValidHit = true;
        }
        // 4. Terrain in Aim Assist (Forgiving Traversal)
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
