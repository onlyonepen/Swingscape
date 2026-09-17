using UnityEngine;

public class FootstepManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody rb;
    [SerializeField] private AudioSource footstepAudioSource;

    [Header("Speed Thresholds")]
    [SerializeField] private float minSpeedThreshold = 1f; // Speed required to start hearing steps
    [SerializeField] private float maxSpeedThreshold = 30f; // Speed where footsteps hit max pace

    [Header("Audio Settings")]
    [SerializeField] private float maxVolume = 1.0f;
    [SerializeField] private float minPitch = 0.8f; // Slow, heavy steps
    [SerializeField] private float maxPitch = 1.8f; // Fast, rapid steps

    [Header("State (Controlled by other scripts)")]
    public bool isPlayingFootsteps = false; // Turn this true when grounded/wallrunning

    private void Start()
    {
        // Ensure the audio source is ready, looping, and silent by default
        if (footstepAudioSource != null)
        {
            footstepAudioSource.loop = true;
            footstepAudioSource.volume = 0f;
            if (!footstepAudioSource.isPlaying)
            {
                footstepAudioSource.Play();
            }
        }
    }

    private void Update()
    {
        float currentSpeed = rb.linearVelocity.magnitude;

        // Check if the switch is ON and the player is actually moving
        if (isPlayingFootsteps && currentSpeed > minSpeedThreshold)
        {
            float t = Mathf.Clamp01(currentSpeed / maxSpeedThreshold);

            // Scale pitch up as speed increases (makes the footsteps play faster)
            if (footstepAudioSource != null)
            {
                footstepAudioSource.pitch = Mathf.Lerp(minPitch, maxPitch, t);
                
                // Fade volume in quickly so it doesn't instantly pop
                footstepAudioSource.volume = Mathf.Lerp(footstepAudioSource.volume, maxVolume, Time.deltaTime * 15f);
            }
        }
        else
        {
            // Fade volume out rapidly if the player stops or leaves the ground/wall
            if (footstepAudioSource != null)
            {
                footstepAudioSource.volume = Mathf.Lerp(footstepAudioSource.volume, 0f, Time.deltaTime * 20f);
            }
        }
    }

    // Call this method from your Movement/GroundCheck scripts
    public void SetFootstepsEnabled(bool state)
    {
        isPlayingFootsteps = state;
    }
}