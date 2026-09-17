using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Pool Settings")]
    [SerializeField] private int poolSize = 30;
    
    [Header("Audio Library")]
    [SerializeField] private List<AudioDataSO> audioLibrary = new List<AudioDataSO>();
    
    // Dictionary for lightning-fast string lookups
    private Dictionary<string, AudioDataSO> audioDictionary;
    private List<AudioSource> audioPool = new List<AudioSource>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(this.gameObject);

        // 1. Initialize the fast-lookup dictionary
        audioDictionary = new Dictionary<string, AudioDataSO>();
        foreach (var audioData in audioLibrary)
        {
            if (!audioDictionary.ContainsKey(audioData.audioName))
            {
                audioDictionary.Add(audioData.audioName, audioData);
            }
            else
            {
                Debug.LogWarning($"AudioManager: Duplicate audio name found! ({audioData.audioName})");
            }
        }

        // 2. Initialize the Audio Pool
        InitializePool();
    }

    private void InitializePool()
    {
        for (int i = 0; i < poolSize; i++)
        {
            GameObject audioObj = new GameObject($"PooledAudioSource_{i}");
            audioObj.transform.SetParent(this.transform);
            
            AudioSource source = audioObj.AddComponent<AudioSource>();
            source.playOnAwake = false;
            
            audioObj.SetActive(false);
            audioPool.Add(source);
        }
    }
    
    /// <summary>
    /// Call this from anywhere using: AudioManager.Instance.PlayAudioByName("Slash", transform.position, true);
    /// </summary>
    public AudioSource PlayAudioByName(string name, Vector3 position, bool randomizePitch = true)
    {
        // Quickly look up the audio data using the dictionary
        if (audioDictionary.TryGetValue(name, out AudioDataSO audioData))
        {
            AudioSource availableSource = GetAvailableSource();
            
            if (availableSource != null)
            {
                availableSource.transform.position = position;
                
                // Apply the base settings from your ScriptableObject
                availableSource.clip = audioData.clip;
                availableSource.volume = audioData.volume;
                availableSource.spatialBlend = audioData.spatialBlend;
                
                // Pitch Randomization Logic
                if (randomizePitch)
                {
                    // Varies the base pitch by +/- 10% for an organic, non-repetitive feel
                    availableSource.pitch = audioData.pitch * Random.Range(0.9f, 1.1f);
                }
                else
                {
                    // Use the exact pitch from the ScriptableObject
                    availableSource.pitch = audioData.pitch;
                }
                
                availableSource.gameObject.SetActive(true);
                availableSource.Play();
                
                StartCoroutine(DisableAfterPlayback(availableSource, audioData.clip.length));
            }
            return availableSource;
        }
        else
        {
            Debug.LogWarning($"AudioManager: Could not find audio with name '{name}' in the library!");
            return null;
        }
    }

    private AudioSource GetAvailableSource()
    {
        for (int i = 0; i < audioPool.Count; i++)
        {
            if (!audioPool[i].gameObject.activeInHierarchy)
            {
                return audioPool[i];
            }
        }
        Debug.LogWarning($"AudioManager: Pool exhausted ({poolSize} sources all busy). Increase poolSize if sounds are dropping.");
        return null;
    }

    private IEnumerator DisableAfterPlayback(AudioSource source, float clipLength)
    {
        yield return new WaitForSeconds(clipLength);
        source.Stop();
        source.clip = null;
        source.gameObject.SetActive(false);
    }
}