using UnityEngine;
using UnityEngine.Rendering.Universal;

public class SpeedLineManager : MonoBehaviour
{
    [Header("Speed Thresholds")]
    [SerializeField] private float speedLineTreshold = 10;
    [SerializeField] private float MaximumSpeedLineTreshold = 40;
    [SerializeField] private Rigidbody rb;

    [Header("Visuals")]
    [SerializeField] private FullScreenPassRendererFeature fullscreenFeature;
    [SerializeField] private Material SpeedLineMat;
    
    [Header("Audio SFX")]
    [SerializeField] private AudioSource windAudioSource;
    [SerializeField] private float maxWindVolume = 1.0f;
    [SerializeField] private float minWindPitch = 0.8f;
    [SerializeField] private float maxWindPitch = 1.5f;

    private Material cacheMat;

    private void Start()
    {
        cacheMat = new Material(SpeedLineMat);
        fullscreenFeature.passMaterial = cacheMat;

        // Ensure the audio source is looping and playing quietly in the background
        if (windAudioSource != null)
        {
            windAudioSource.loop = true;
            windAudioSource.volume = 0f;
            if (!windAudioSource.isPlaying)
            {
                windAudioSource.Play();
            }
        }
    }

    private void OnDestroy()
    {
        Destroy(cacheMat);
    }

    private void Update()
    {
        if (rb.linearVelocity.magnitude > speedLineTreshold)
        {
            float currentSpeed = rb.linearVelocity.magnitude;
            float t = Mathf.Clamp01(currentSpeed / MaximumSpeedLineTreshold);
            
            // --- Update Visuals ---
            float maskSize = Mathf.Lerp(1.0f, 0.6f, t);
            float maskContrast = Mathf.Lerp(1.0f, 0.6f, t);

            cacheMat.SetFloat("_Mask_size", maskSize);
            cacheMat.SetFloat("_Mask_Contrast", maskContrast);

            // --- Update Audio ---
            if (windAudioSource != null)
            {
                // Smoothly lerp the volume up so it doesn't pop when crossing the threshold
                windAudioSource.volume = Mathf.Lerp(windAudioSource.volume, Mathf.Lerp(0f, maxWindVolume, t), Time.deltaTime * 10f);
                windAudioSource.pitch = Mathf.Lerp(minWindPitch, maxWindPitch, t);
            }
        }
        else
        {
            // --- Reset Visuals ---
            cacheMat.SetFloat("_Mask_size", 1);
            cacheMat.SetFloat("_Mask_Contrast", 1);

            // --- Reset Audio ---
            if (windAudioSource != null)
            {
                // Smoothly fade the wind out when the player slows down
                windAudioSource.volume = Mathf.Lerp(windAudioSource.volume, 0f, Time.deltaTime * 10f);
                windAudioSource.pitch = Mathf.Lerp(windAudioSource.pitch, minWindPitch, Time.deltaTime * 5f);
            }
        }
    }
}