using JL.Splitting;
using UnityEngine;

[RequireComponent(typeof(Attackable))]
public class ContinueSplittable : MonoBehaviour, ISliceable
{
    [Header("Slice Juice")]
    [SerializeField] private float pushForce = 5f; // Tweak this in the inspector for stronger/weaker pushes

    public void OnSliceStart() { }

    public void OnSliceComplete(SplitResult result)
    {
        bool hasPlayer = GlobalReference.Instance != null && GlobalReference.Instance.player != null;
        Vector3 playerPosition = hasPlayer ? GlobalReference.Instance.player.transform.position : Vector3.zero;

        if (!hasPlayer)
        {
            Debug.LogWarning("GlobalReference.Instance or player is missing! Pieces won't be pushed outward.");
        }

        PushApart(result.posObject, playerPosition, hasPlayer);
        PushApart(result.negObject, playerPosition, hasPlayer);
    }

    private void PushApart(GameObject splitObject, Vector3 playerPosition, bool hasPlayer)
    {
        if (splitObject == null) return;

        Rigidbody[] rigidbodies = splitObject.GetComponentsInChildren<Rigidbody>();
        if (rigidbodies.Length == 0)
        {
            Debug.LogWarning($"No Rigidbodies found on {splitObject.name} or its children");
            return;
        }

        foreach (Rigidbody rb in rigidbodies)
        {
            rb.constraints = RigidbodyConstraints.None;

            if (hasPlayer)
            {
                Vector3 pushDir = (rb.transform.position - playerPosition).normalized;
                rb.AddForce(pushDir * pushForce, ForceMode.Impulse);
            }
        }
    }
}
