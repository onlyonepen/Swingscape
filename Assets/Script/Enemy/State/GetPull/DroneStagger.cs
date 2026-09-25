using UnityEngine;

namespace Script.Enemy.State.GetPull
{
    public class DroneStagger : EnemyBaseState
    {
        public DroneStagger(BaseEnemy enemy)
        {
            Enemy = enemy;
        }

        public override void OnStateEnter()
        {
            base.OnStateEnter();
            Enemy.rb.constraints = RigidbodyConstraints.FreezeRotation;
        }

        public override void OnStateUpdate()
        {
            if (Time.time - StateEnterTime >= Enemy.staggerTime)
            {
                if(seePlayer()) Enemy.ChangeState(Enemy.stateFactory.CreateAggroState(Enemy));
                else Enemy.ChangeState(Enemy.stateFactory.CreateIdleState(Enemy));
            }
        }
        
        private bool seePlayer()
        {
            GameObject player = GlobalReference.Instance.player.gameObject;

            float playerDist = Vector3.Distance(player.transform.position, Enemy.transform.position);
            if (playerDist > Enemy.Stat.DetectionRange) return false;

            Vector3 toPlayer = player.transform.position - Enemy.transform.position;
            return Physics.Raycast(Enemy.transform.position, toPlayer, playerDist + 5f, GlobalReference.Instance.playerLayer);
        }
    }
}