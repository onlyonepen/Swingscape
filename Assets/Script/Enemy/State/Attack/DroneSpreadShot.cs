using UnityEngine;

namespace Script.Enemy.State.Attack
{
    public class DroneSpreadShot : EnemyBaseState
    {
        private bool shoted;
        
        private float Anticipation = 1.5f;
        private float Recovery = 0.2f;
        
        public DroneSpreadShot(BaseEnemy enemy)
        {
            Enemy = enemy;
        }

        public override void OnStateEnter()
        {
            base.OnStateEnter();
            shoted = false;
            Enemy.rb.constraints = RigidbodyConstraints.FreezeAll;
        }

        public override void OnStateUpdate()
        {
            base.OnStateUpdate();
            if(Time.time - StateEnterTime > Anticipation && !shoted)
            {
                shoted = true;
                shootProjectile();
            }

            if(Time.time - StateEnterTime > Anticipation + Recovery) Enemy.ChangeState(Enemy.stateFactory.CreateAggroState(Enemy));
        }

        private void shootProjectile()
        {
            Enemy.Guntip.LookAt(GlobalReference.Instance.player.transform.position);
            Quaternion centerRotation = Enemy.Guntip.rotation;

            float randomSpreadAngle = Random.Range(5f, 35f);

            float randomAxisTilt = Random.Range(0f, 360f);
            Vector3 randomSpreadAxis = Quaternion.AngleAxis(randomAxisTilt, Enemy.Guntip.forward) * Enemy.Guntip.up;

            Quaternion leftRotation = Quaternion.AngleAxis(-randomSpreadAngle, randomSpreadAxis) * centerRotation;
            Quaternion rightRotation = Quaternion.AngleAxis(randomSpreadAngle, randomSpreadAxis) * centerRotation;

            summonProjectile(centerRotation);
            summonProjectile(leftRotation);
            summonProjectile(rightRotation);
        }

        private void summonProjectile(Quaternion Spawnrotation)
        {
            GameObject bullet = GameObject.Instantiate(Enemy.ProjectilePrefab, Enemy.Guntip.position, Spawnrotation);
            bullet.GetComponent<BasicEnemyProjectile>().ProjectileOwner = Enemy;
        }
    }
    
}