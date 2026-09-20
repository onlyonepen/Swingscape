using UnityEngine;

/// <summary>Shared quadratic-formula projectile solver: given a start/end point and a desired
/// arc apex height, returns the launch velocity that lands exactly on the end point.
/// Same derivation GrappleLeapState uses for its grapple-leap arc.</summary>
public static class ProjectileMath
{
    public static Vector3 CalculateArcVelocity(Vector3 startPoint, Vector3 endPoint, float trajectoryHeight)
    {
        float gravity = Physics.gravity.y;
        float displacementY = endPoint.y - startPoint.y;
        Vector3 displacementXZ = new Vector3(endPoint.x - startPoint.x, 0f, endPoint.z - startPoint.z);

        float optimizedHeight = Mathf.Max(displacementY + 0.1f, trajectoryHeight);

        Vector3 velocityY = Vector3.up * Mathf.Sqrt(-2f * gravity * optimizedHeight);

        float timeUp = Mathf.Sqrt(-2f * optimizedHeight / gravity);
        float timeDown = Mathf.Sqrt(2f * (displacementY - optimizedHeight) / gravity);

        Vector3 velocityXZ = displacementXZ / (timeUp + timeDown);

        return velocityXZ + velocityY;
    }
}
