using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Audio Library", menuName = "Swingscape/Audio Library")]
public class AudioLibrarySO : ScriptableObject
{
    public List<AudioDataSO> audioClips = new List<AudioDataSO>();
}
