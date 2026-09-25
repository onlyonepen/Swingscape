using UnityEngine;

namespace Script.Enemy.State.Idle
{
    public class DroneIdle : EnemyBaseState
    {
        private float detectionValue;
        private float toAggroTime = 1f;
        
        public DroneIdle(BaseEnemy enemy)
        {
            Enemy = enemy;
        }
        
        public override void OnStateEnter()
        {
            base.OnStateEnter();
            Enemy.rb.linearVelocity = Vector3.zero;
            Enemy.rb.constraints = RigidbodyConstraints.FreezeAll;
        }

        public override void OnStateUpdate()
        {
            playerCheck();
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