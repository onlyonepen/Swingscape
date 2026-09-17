using UnityEngine;

/// <summary>
/// Owns grapple target selection: the aim-assisted ray/sphere cast that decides what
/// the player will grapple, the layer masks that classify grapple targets, and the
/// on-screen prediction reticle. Extracted from PlayerStateManager so the state
/// machine stays focused on state logic.
/// </summary>
public class GrappleTargeting : MonoBehaviour
{
    [Header("Reticle")]
    public Transform predictionPoint;

    [Header("Range & target layers")]
    public float GrappleMaxDistance;
    public LayerMask Swingable;
    public LayerMask Pullable;
    public LayerMask HeavyPull;

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
        LayerMask assistPriority = HeavyPull | Pullable; // Enemies / Pullables
        LayerMask allGrappleMasks = Swingable | assistPriority;
        LayerMask obstacleMask = GlobalReference.Instance.TerrainLayer;

        // --- 1. DIRECT RAYCAST ---
        RaycastHit directHitEnemy = new RaycastHit();
        bool foundDirectEnemy = false;

        RaycastHit directHitSwing = new RaycastHit();
        bool foundDirectSwing = false;

        // Check perfectly down the center first
        if (Physics.Raycast(cam.transform.position, cam.transform.forward, out RaycastHit tempDirect, GrappleMaxDistance, allGrappleMasks | obstacleMask))
        {
            int hitLayer = 1 << tempDirect.collider.gameObject.layer;

            // SWAPPED: Check if the direct hit is terrain/swingable FIRST
            if ((hitLayer & Swingable) != 0)
            {
                directHitSwing = tempDirect;
                foundDirectSwing = true;
            }
            // Then check if the direct hit is an enemy
            else if ((hitLayer & assistPriority) != 0)
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
            allGrappleMasks
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

            Vector3 directionToHit = localHitPoint.normalized;
            float alignmentScore = Vector3.Dot(cam.transform.forward, directionToHit);

            bool isEnemy = ((1 << hit.collider.gameObject.layer) & assistPriority) != 0;

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
        if (hasValidHit)
        {
            predictionPoint.gameObject.SetActive(true);
            predictionPoint.position = finalHit.point;
        }
        else
        {
            predictionPoint.gameObject.SetActive(false);
        }

        return finalHit;
    }
}
