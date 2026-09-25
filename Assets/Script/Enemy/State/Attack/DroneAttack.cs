using DG.Tweening;
using UnityEngine;

namespace Script.Enemy.State.Attack
{
    public class DroneAttack : EnemyBaseState
    {
        private bool shoted;
        
        private float Anticipation = 1f;
        private float Recovery = .2f;
        
        private AudioSource ChargeUpSound;

        public DroneAttack(BaseEnemy enemy)
        {
            Enemy = enemy;
        }
        
        public override void OnStateEnter()
        {
            base.OnStateEnter();
            shoted = false;
            Enemy.rb.constraints = RigidbodyConstraints.FreezeAll;
            Enemy.ChargeUpParticles.Play();
            
            ChargeUpSound = AudioManager.Instance.PlayAudioByName("DroneCharge", Enemy.transform.position);
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
            
            if(!shoted) LookAtPlayer();
        }

        public override void OnStateExit()
        {
            Enemy.ChargeUpParticles.Stop();
        }

        private void shootProjectile()
        {
            Enemy.Guntip.LookAt(GlobalReference.Instance.player.transform.position);
            GameObject bullet = GameObject.Instantiate(Enemy.ProjectilePrefab, Enemy.Guntip.position, Enemy.Guntip.rotation);
            bullet.GetComponent<BasicEnemyProjectile>().ProjectileOwner = Enemy;
            Enemy.ChargeUpParticles.Stop();
            ChargeUpSound.Stop();
            AudioManager.Instance.PlayAudioByName("DroneShot", Enemy.transform.position);
        }

        private void LookAtPlayer()
        {
            Vector3 dronePos = Enemy.transform.position;
            Vector3 playerPos = GlobalReference.Instance.player.gameObject.transform.position;

            Vector3 directionToPlayer = (playerPos - dronePos).normalized;

            Quaternion targetRotation = Quaternion.LookRotation(directionToPlayer);
            Enemy.transform.rotation = Quaternion.Slerp(Enemy.transform.rotation, targetRotation, Time.deltaTime * 5f);
        }
    }
}