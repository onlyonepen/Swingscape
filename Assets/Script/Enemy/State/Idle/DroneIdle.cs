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
        private Vector3 currentHeading;

        // Tracked separately from the rendered transform so the sine bob (applied only to the
        // transform below) never feeds back into the arrival/steering distance checks.
        private Vector3 navPosition;
        private float bobPhase;

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
            currentHeading = Enemy.transform.forward;
            navPosition = Enemy.transform.position;
            bobPhase = Random.Range(0f, Mathf.PI * 2f);
        }

        public override void OnStateUpdate()
        {
            playerCheck();
            Wander();
        }

        private void Wander()
        {
            if (!hasWanderTarget)
            {
                PickNewWanderTarget();
            }

            Vector3 toTarget = wanderTarget - navPosition;

            if (toTarget.magnitude <= ArrivalThreshold)
            {
                hasWanderTarget = false;
            }
            else
            {
                Vector3 desiredDir = toTarget.normalized;
                float maxRadiansDelta = Enemy.Stat.WanderTurnSpeed * Mathf.Deg2Rad * Time.deltaTime;
                currentHeading = Vector3.RotateTowards(currentHeading, desiredDir, maxRadiansDelta, 0f).normalized;

                // Bail into a fresh target instead of pushing through obstacles — this is just
                // idle flavor movement, not navigation, so it doesn't need to route around things.
                LayerMask obstacleMask = ~GlobalReference.Instance.playerLayer;
                if (Physics.Raycast(navPosition, currentHeading, 2f, obstacleMask))
                {
                    hasWanderTarget = false;
                }
                else
                {
                    navPosition += currentHeading * (Enemy.Stat.WanderSpeed * Time.deltaTime);
                    Enemy.transform.rotation = Quaternion.LookRotation(currentHeading);
                }
            }

            bobPhase += Time.deltaTime * Enemy.Stat.WanderBobSpeed * Mathf.PI * 2f;
            float bobOffset = Mathf.Sin(bobPhase) * Enemy.Stat.WanderBobAmplitude;
            Enemy.transform.position = navPosition + Vector3.up * bobOffset;
        }

        private const int WanderTargetAttempts = 8;

        private void PickNewWanderTarget()
        {
            LayerMask obstacleMask = ~GlobalReference.Instance.playerLayer;

            for (int attempt = 0; attempt < WanderTargetAttempts; attempt++)
            {
                Vector2 randomCircle = Random.insideUnitCircle * Enemy.Stat.WanderLeash;
                float heightOffset = Random.Range(-Enemy.Stat.WanderHeightRange, Enemy.Stat.WanderHeightRange);
                Vector3 candidate = Enemy.SpawnPosition + new Vector3(randomCircle.x, heightOffset, randomCircle.y);

                Vector3 toCandidate = candidate - navPosition;
                float distance = toCandidate.magnitude;

                // Only commit to targets with a clear straight-line path, so a blocked pick
                // doesn't just send it bumping into the same wall from a different angle.
                if (distance <= Mathf.Epsilon || !Physics.Raycast(navPosition, toCandidate.normalized, distance, obstacleMask))
                {
                    wanderTarget = candidate;
                    hasWanderTarget = true;
                    return;
                }
            }

            // Every candidate this pass was blocked (e.g. a tight room) — spawn itself is always
            // a safe fallback since that's where the enemy started out in the open.
            wanderTarget = Enemy.SpawnPosition;
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