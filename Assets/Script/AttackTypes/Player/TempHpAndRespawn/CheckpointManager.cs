using UnityEngine;

/// <summary>
/// Tracks the player's current respawn anchor. CheckpointTrigger volumes placed at
/// each POI call SetCheckpoint as the player reaches them; DiedState teleports back
/// here on respawn instead of reloading the scene.
/// </summary>
public class CheckpointManager : MonoBehaviour
{
    public static CheckpointManager Instance { get; private set; }

    [Tooltip("Used as the checkpoint until the player reaches their first CheckpointTrigger.")]
    public Transform StartPoint;

    public Transform CurrentCheckpoint { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        CurrentCheckpoint = StartPoint;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void SetCheckpoint(Transform checkpoint)
    {
        CurrentCheckpoint = checkpoint;
    }
}

/// <summary>Flags that persist across a scene reload within the same playthrough.</summary>
public static class GameValue
{
    public static bool ObtainedGrapple = false;
}
