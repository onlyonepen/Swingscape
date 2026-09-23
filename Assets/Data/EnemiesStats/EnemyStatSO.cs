using UnityEngine;

namespace Script.Enemy.EnemiesStats
{
    [CreateAssetMenu(fileName = "EnemyName", menuName = "EnemyStat")]
    public class EnemyStatSO : ScriptableObject
    {
        public float DetectionRange;
        public float MoveSpeed;
        public float AttackFrequentcy;

        [Tooltip("Max vertical distance (above or below the player) an aggro'd enemy will try to close. Beyond this, it holds its current elevation instead of continuing to climb/dive.")]
        public float MaxElevationOffset = 15f;
    }
}