using System.Collections.Generic;
using UnityEngine;

public class PlayerAttackArea : MonoBehaviour
{
    [Header("Hitbox Settings")]
    [SerializeField] private LayerMask attackableLayer;
    
    // We broke these out to act exactly like a Capsule's Height/Radius!
    [Tooltip("How far forward the attack reaches (Capsule Height)")]
    [SerializeField] private float swingReach = 3f; 
    
    [Tooltip("How wide the attack sweeps side-to-side (Capsule Radius)")]
    [SerializeField] private float swingWidth = 2f; 
    
    [Tooltip("How thick the blade plane is (Keep this thin for a sword)")]
    [SerializeField] private float swingThickness = 0.5f; 

    [Header("References")]
    [SerializeField] private Transform hitboxCenter; 

    /// <summary>
    /// Call this method from your attacking script on the exact frame the swing connects.
    /// </summary>
    public GameObject[] GetTargetsInSwing()
    {
        // Construct the size vector dynamically from our separated floats
        Vector3 boxDimensions = new Vector3(swingWidth, swingThickness, swingReach);

        // Perform an instantaneous physics check
        Collider[] hits = Physics.OverlapBox(
            hitboxCenter.position,
            boxDimensions / 2f, // OverlapBox requires half-extents (size divided by 2)
            hitboxCenter.rotation,
            attackableLayer
        );

        List<GameObject> validTargets = new List<GameObject>();
        foreach (Collider hit in hits)
        {
            if (hit != null && hit.gameObject.activeInHierarchy)
            {
                validTargets.Add(hit.gameObject);
            }
        }
        
        return validTargets.ToArray();
    }

    // Draws a red outline in the Editor so you can physically see the reach!
    private void OnDrawGizmosSelected()
    {
        if (hitboxCenter != null)
        {
            Vector3 boxDimensions = new Vector3(swingWidth, swingThickness, swingReach);
            
            Gizmos.color = new Color(1, 0, 0, 0.5f);
            Gizmos.matrix = Matrix4x4.TRS(hitboxCenter.position, hitboxCenter.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, boxDimensions);
        }
    }
}