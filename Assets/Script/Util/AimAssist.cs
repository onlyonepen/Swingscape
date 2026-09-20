using System;
using UnityEngine;

/// <summary>Shared aim-assist cone/spherecast target selection: the same "direct raycast first,
/// then widening spherecast cone scored by alignment" algorithm GrappleTargeting uses to pick
/// grapple targets, generalized so other systems (e.g. knockback retargeting) can reuse it
/// against their own layer mask instead of duplicating the cast/scoring logic.</summary>
public static class AimAssist
{
    public static bool TryFindTarget(
        Vector3 origin, Vector3 direction, float maxDistance,
        float minRadius, float maxRadius,
        LayerMask queryMask, LayerMask obstacleMask,
        Func<GameObject, bool> isValidCandidate,
        out RaycastHit result)
    {
        direction.Normalize();

        // Direct raycast down the center wins outright, same priority order as grapple targeting.
        if (Physics.Raycast(origin, direction, out RaycastHit directHit, maxDistance, queryMask)
            && isValidCandidate(directHit.collider.gameObject))
        {
            result = directHit;
            return true;
        }

        RaycastHit[] hits = Physics.SphereCastAll(origin, maxRadius, direction, maxDistance, queryMask);

        bool found = false;
        RaycastHit best = new RaycastHit();
        float bestScore = -1f;

        foreach (RaycastHit hit in hits)
        {
            // Unity quirk: a spherecast starting inside a collider reports hit.point as zero.
            if (hit.point == Vector3.zero) continue;
            if (!isValidCandidate(hit.collider.gameObject)) continue;

            Vector3 localHitPoint = hit.point - origin;
            float distanceAlongRay = Vector3.Dot(localHitPoint, direction);
            if (distanceAlongRay < 0) continue;

            float allowedRadius = Mathf.Lerp(minRadius, maxRadius, distanceAlongRay / maxDistance);
            Vector3 pointOnCenterLine = origin + direction * distanceAlongRay;
            if (Vector3.Distance(pointOnCenterLine, hit.point) > allowedRadius) continue;

            if (Physics.Linecast(origin, hit.point, out RaycastHit blockHit, obstacleMask)
                && blockHit.collider != hit.collider) continue;

            float score = Vector3.Dot(direction, localHitPoint.normalized);
            if (!found || score > bestScore)
            {
                best = hit;
                bestScore = score;
                found = true;
            }
        }

        result = best;
        return found;
    }
}
