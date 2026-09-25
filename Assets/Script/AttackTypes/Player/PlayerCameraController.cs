using UnityEngine;

public class PlayerCameraController : MonoBehaviour
{
    public Camera[] AllPlayerCamera;

    public void changeFov(float fov)
    {
        foreach (Camera c in AllPlayerCamera)
        {
            c.fieldOfView = fov;
        }
    }
}
