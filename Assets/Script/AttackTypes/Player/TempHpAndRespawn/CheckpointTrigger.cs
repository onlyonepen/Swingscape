using UnityEngine;

/// <summary>Drop one at each POI/story beat. First touch by the player sets it as
/// their respawn anchor in CheckpointManager.</summary>
public class CheckpointTrigger : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        CheckpointManager.Instance.SetCheckpoint(transform);
    }
}
