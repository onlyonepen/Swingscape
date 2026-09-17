using UnityEngine;

namespace Script.Enemy.EnemiesStats
{
    [CreateAssetMenu(fileName = "EnemyName", menuName = "EnemyStat")]
    public class EnemyStatSO : ScriptableObject
    {
        public float DetectionRange;
        public float MoveSpeed;
        public float AttackFrequentcy;
    }
}