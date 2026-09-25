using UnityEngine;

[CreateAssetMenu(fileName = "New Player Health Stats", menuName = "Swingscape/Player Stats/Health")]
public class PlayerHealthStatsSO : ScriptableObject
{
    public int maxHp = 3;

    [Header("Invulnerability")]
    public float iFrameDuration = 0.5f;
}
