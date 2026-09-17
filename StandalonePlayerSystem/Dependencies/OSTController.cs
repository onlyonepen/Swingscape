using System.Collections;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class OSTController : MonoBehaviour
{
    [Header("OST Settings")]
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private float fadeDuration = 3f;
    [SerializeField] private float maxVolume = 1f;

    private AudioSource audioSource;
    private Coroutine ostCoroutine;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        // We handle the loop manually to accommodate the fades, so disable native loop
        audioSource.loop = false; 
    }

    private void Start()
    {
        if (playOnStart)
        {
            PlayOST();
        }
    }

    public void PlayOST()
    {
        if (audioSource.clip == null)
        {
            Debug.LogWarning("OST Controller: No AudioClip assigned to the AudioSource!");
            return;
        }

        if (ostCoroutine != null) StopCoroutine(ostCoroutine);
        ostCoroutine = StartCoroutine(OSTLoopRoutine());
    }

    public void StopOST() // Optional: Call this if you need to stop the music mid-level
    {
        if (ostCoroutine != null) StopCoroutine(ostCoroutine);
        StartCoroutine(FadeOutAndStop());
    }

    private IEnumerator OSTLoopRoutine()
    {
        // Safety check: if the track is extremely short, prevent the fades from overlapping
        float actualFadeDuration = fadeDuration;
        if (audioSource.clip.length < fadeDuration * 2f)
        {
            actualFadeDuration = audioSource.clip.length / 2f;
        }

        while (true) // Infinite loop structure
        {
            audioSource.volume = 0f;
            audioSource.Play();

            // 1. Fade In (Start of Loop)
            float timer = 0f;
            while (timer < actualFadeDuration)
            {
                timer += Time.unscaledDeltaTime;
                audioSource.volume = Mathf.Lerp(0f, maxVolume, timer / actualFadeDuration);
                yield return null;
            }
            audioSource.volume = maxVolume;

            // 2. Play Main Track (Wait until it is time to fade out)
            // Subtract both the fade-in time we just did AND the upcoming fade-out time
            float waitTime = audioSource.clip.length - (actualFadeDuration * 2f);
            if (waitTime > 0)
            {
                yield return new WaitForSecondsRealtime(waitTime);
            }

            // 3. Fade Out (End of Loop)
            timer = 0f;
            while (timer < actualFadeDuration)
            {
                timer += Time.unscaledDeltaTime;
                audioSource.volume = Mathf.Lerp(maxVolume, 0f, timer / actualFadeDuration);
                yield return null;
            }
            
            audioSource.volume = 0f;
            audioSource.Stop();

            // The while(true) loop immediately restarts, triggering the next Fade In!
        }
    }

    private IEnumerator FadeOutAndStop()
    {
        float startVol = audioSource.volume;
        float timer = 0f;
        while (timer < fadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            audioSource.volume = Mathf.Lerp(startVol, 0f, timer / fadeDuration);
            yield return null;
        }
        audioSource.volume = 0f;
        audioSource.Stop();
    }
}