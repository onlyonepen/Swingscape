using System;
using UnityEngine;

public class VolumeController : MonoBehaviour
{
    public float GlobalVolume = 0.6f;
    // Call this method from a UI Slider's OnValueChanged event
    // Ensure your slider goes from 0.0 to 1.0
    private void Start()
    {
        SetGlobalVolume(GlobalVolume);
    }

    public void SetGlobalVolume(float volume)
    {
        SceneTransitionManager.SetMasterVolume(volume);
    }
}