using JL.Splitting;
using UnityEngine;

[RequireComponent(typeof(Attackable))]
public class ContinueSplittable : MonoBehaviour, IDamagable
{
    [Header("Slice Juice")]
    [SerializeField] private float pushForce = 5f; // Tweak this in the inspector for stronger/weaker pushes

    public void SplitDeath(Transform plane)
    {
        PointPlane PPData = new PointPlane(plane.position, plane.rotation);

        // 1. Safely attempt to get the Splittable component
        if (TryGetComponent<Splittable>(out var splittable))
        {
            splittable.SplitAsync(PPData, (SplitResult result) =>
            {
                // Cache the player position safely to avoid redundant lookups inside the blocks
                Vector3 playerPosition = Vector3.zero;
                bool hasPlayer = GlobalReference.Instance != null && GlobalReference.Instance.player != null;
                
                if (hasPlayer)
                {
                    playerPosition = GlobalReference.Instance.player.transform.position;
                }
                else
                {
                    Debug.LogWarning("globalreference.instance or player is missing! Pieces won't be pushed outward.");
                }

                // 3. Process the positive object AND all its children
                if (result.posObject != null)
                {
                    // Get all rigidbodies on this object and any nested children
                    Rigidbody[] posRigidbodies = result.posObject.GetComponentsInChildren<Rigidbody>();
                    
                    if (posRigidbodies.Length > 0)
                    {
                        foreach (Rigidbody rb in posRigidbodies)
                        {
                            rb.constraints = RigidbodyConstraints.None;
                            
                            if (hasPlayer)
                            {
                                // Calculate direction based on the specific child's position
                                Vector3 pushDir = (rb.transform.position - playerPosition).normalized;
                                rb.AddForce(pushDir * pushForce, ForceMode.Impulse);
                            }
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"No Rigidbodies found on posObject or its children: {result.posObject.name}");
                    }
                }

                // 4. Process the negative object AND all its children
                if (result.negObject != null)
                {
                    // Get all rigidbodies on this object and any nested children
                    Rigidbody[] negRigidbodies = result.negObject.GetComponentsInChildren<Rigidbody>();

                    if (negRigidbodies.Length > 0)
                    {
                        foreach (Rigidbody rb in negRigidbodies)
                        {
                            rb.constraints = RigidbodyConstraints.None;
                            
                            if (hasPlayer)
                            {
                                // Calculate direction based on the specific child's position
                                Vector3 pushDir = (rb.transform.position - playerPosition).normalized;
                                rb.AddForce(pushDir * pushForce, ForceMode.Impulse);
                            }
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"No Rigidbodies found on negObject or its children: {result.negObject.name}");
                    }
                }
            });
        }
        else
        {
            Debug.LogError($"Splittable component missing on {gameObject.name}. Cannot execute SplitAsync.");
        }
    }
}