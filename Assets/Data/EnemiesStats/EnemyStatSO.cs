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

        [Tooltip("How far from its spawn point an idle enemy will wander before picking a target back toward spawn.")]
        public float WanderLeash = 30f;

        [Tooltip("Movement speed while idly wandering (separate from MoveSpeed, which is used during Aggro strafing).")]
        public float WanderSpeed = 1.5f;

        [Tooltip("How far above or below spawn height an idle enemy's wander target can land.")]
        public float WanderHeightRange = 5f;

        [Tooltip("Degrees per second an idly wandering enemy can steer its heading. Lower values make it swing wide, curved turns instead of snapping straight at a new target.")]
        public float WanderTurnSpeed = 60f;

        [Tooltip("How far up/down an idly wandering enemy bobs, layered on top of its wander movement.")]
        public float WanderBobAmplitude = 0.3f;

        [Tooltip("Bob cycles per second while idly wandering.")]
        public float WanderBobSpeed = 0.5f;
    }
}