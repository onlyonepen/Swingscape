using UnityEngine;

namespace Script.Enemy.State.Idle
{
    public class DroneIdle : EnemyBaseState
    {
        private float detectionValue;
        private float toAggroTime = 1f;

        private const float ArrivalThreshold = 0.5f;

        private Vector3 wanderTarget;
        private bool hasWanderTarget;

        public DroneIdle(BaseEnemy enemy)
        {
            Enemy = enemy;
        }

        public override void OnStateEnter()
        {
            base.OnStateEnter();
            Enemy.rb.linearVelocity = Vector3.zero;
            Enemy.rb.constraints = RigidbodyConstraints.FreezeAll;
            hasWanderTarget = false;
        }

        public override void OnStateUpdate()
        {
            playerCheck();
            Wander();
        }

        private void Wander()
        {
            Vector3 dronePos = Enemy.transform.position;

            if (!hasWanderTarget)
            {
                PickNewWanderTarget();
            }

            Vector3 toTarget = wanderTarget - dronePos;

            if (toTarget.magnitude <= ArrivalThreshold)
            {
                hasWanderTarget = false;
                return;
            }

            Vector3 moveDir = toTarget.normalized;

            // Bail into a fresh target instead of pushing through obstacles — this is just
            // idle flavor movement, not navigation, so it doesn't need to route around things.
            LayerMask obstacleMask = ~GlobalReference.Instance.playerLayer;
            if (Physics.Raycast(dronePos, moveDir, 2f, obstacleMask))
            {
                hasWanderTarget = false;
                return;
            }

            Enemy.transform.position += moveDir * (Enemy.Stat.WanderSpeed * Time.deltaTime);

            Quaternion targetRotation = Quaternion.LookRotation(moveDir);
            Enemy.transform.rotation = Quaternion.Slerp(Enemy.transform.rotation, targetRotation, Time.deltaTime * 5f);
        }

        private void PickNewWanderTarget()
        {
            Vector2 randomCircle = Random.insideUnitCircle * Enemy.Stat.WanderLeash;
            float heightOffset = Random.Range(-Enemy.Stat.WanderHeightRange, Enemy.Stat.WanderHeightRange);
            wanderTarget = Enemy.SpawnPosition + new Vector3(randomCircle.x, heightOffset, randomCircle.y);
            hasWanderTarget = true;
        }

        private void playerCheck()
        {
            if (GlobalReference.Instance == null) return;
            GameObject player = GlobalReference.Instance.player.gameObject;

            float playerDist = Vector3.Distance(player.transform.position, Enemy.transform.position);
            if (playerDist > Enemy.Stat.DetectionRange) return;

            LayerMask playerAndGround = GlobalReference.Instance.playerLayer | GlobalReference.Instance.TerrainLayer;
    
            // Normalize the direction vector for accurate raycasting
            Vector3 toPlayer = (player.transform.position - Enemy.transform.position).normalized;
    
            bool seePLayer = false;

            // Cast the ray against BOTH the terrain and the player
            if (Physics.Raycast(Enemy.transform.position, toPlayer, out RaycastHit hit, playerDist + 5f, playerAndGround))
            {
                // If the very first object the ray hits is the player, they have line of sight!
                // If it hits a wall first, this remains false.
                if (((1 << hit.collider.gameObject.layer) & GlobalReference.Instance.playerLayer) != 0)
                {
                    seePLayer = true;
                }
            }

            if (seePLayer) detectionValue += Time.deltaTime;
            else detectionValue -= Time.deltaTime;
    
            if ((seePLayer && detectionValue > toAggroTime) || playerDist < 15) 
                Enemy.ChangeState(Enemy.stateFactory.CreateAggroState(Enemy));
        }
    }
}