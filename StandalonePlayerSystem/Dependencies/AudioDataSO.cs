using UnityEngine;

[CreateAssetMenu(fileName = "New Audio Data", menuName = "Swingscape/Audio Data")]
public class AudioDataSO : ScriptableObject
{
    [Tooltip("The string name you will use to call this audio (e.g., 'SwordSwing', 'DroneExplosion')")]
    public string audioName;

    [Header("Audio File & Settings")]
    public AudioClip clip;
    
    [Range(0f, 1f)]
    public float volume = 1f;
    
    [Range(0.1f, 3f)]
    public float pitch = 1f;
    
    [Range(0f, 1f)]
    [Tooltip("0 = 2D Sound (UI/Music), 1 = 3D Sound (World FX)")]
    public float spatialBlend = 1f; 
}