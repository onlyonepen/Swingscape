using UnityEngine;

[CreateAssetMenu(fileName = "New Player Camera Stats", menuName = "Swingscape/Player Stats/Camera")]
public class PlayerCameraStatsSO : ScriptableObject
{
    [Header("Look")]
    public float fov = 60f;
    public bool invertCamera = false;
    public float mouseSensitivity = 2f;
    public float maxLookAngle = 50f;

    [Header("Speed-based FOV")]
    public float minFov = 80f;
    public float maxFov = 100f;
    public float fovSmoothSpeed = 10f;
    public float fovChangeTreshold = 10f;
    public float MaxSpeedForFovChange = 60f;
}
