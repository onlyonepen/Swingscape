using DG.Tweening;
using UnityEngine;

namespace Script.Enemy.State.Attack
{
    public class DroneAttack : EnemyBaseState
    {
        private const int BurstCount = 3;
        private const float BurstInterval = 0.2f;

        private int shotsFired;
        private float nextShotTime;

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
            shotsFired = 0;
            nextShotTime = StateEnterTime + Anticipation;
            Enemy.rb.constraints = RigidbodyConstraints.FreezeAll;
            Enemy.ChargeUpParticles.Play();

            ChargeUpSound = AudioManager.Instance.PlayAudioByName("DroneCharge", Enemy.transform.position);
        }

        public override void OnStateUpdate()
        {
            base.OnStateUpdate();

            if (GlobalReference.Instance?.player == null)
            {
                Enemy.ChangeState(Enemy.stateFactory.CreateIdleState(Enemy));
                return;
            }

            Vector3 predictedPosition = GetPredictedPosition();

            if (shotsFired < BurstCount && Time.time >= nextShotTime)
            {
                shootProjectile(predictedPosition);
                shotsFired++;
                nextShotTime = Time.time + BurstInterval;
            }

            if (shotsFired >= BurstCount && Time.time >= nextShotTime + Recovery)
            {
                Enemy.ChangeState(Enemy.stateFactory.CreateAggroState(Enemy));
            }

            if (shotsFired < BurstCount) LookAtPredictedPoint(predictedPosition);
        }

        public override void OnStateExit()
        {
            Enemy.ChargeUpParticles.Stop();
        }

        private Vector3 GetPredictedPosition()
        {
            PlayerManager player = GlobalReference.Instance.player;
            float bulletSpeed = Enemy.ProjectilePrefab.GetComponent<BadBeeProjectile>().speed;
            float distance = Vector3.Distance(Enemy.Guntip.position, player.transform.position);
            float leadTime = distance / bulletSpeed;
            return player.transform.position + player.rb.linearVelocity * leadTime;
        }

        private void shootProjectile(Vector3 predictedPosition)
        {
            Enemy.Guntip.LookAt(predictedPosition);
            GameObject bullet = GameObject.Instantiate(Enemy.ProjectilePrefab, Enemy.Guntip.position, Enemy.Guntip.rotation);
            bullet.GetComponent<BadBeeProjectile>().ProjectileOwner = Enemy;

            if (shotsFired == 0)
            {
                Enemy.ChargeUpParticles.Stop();
                ChargeUpSound.Stop();
            }
            AudioManager.Instance.PlayAudioByName("DroneShot", Enemy.transform.position);
        }

        private void LookAtPredictedPoint(Vector3 predictedPosition)
        {
            Vector3 dronePos = Enemy.transform.position;
            Vector3 directionToPoint = (predictedPosition - dronePos).normalized;

            Quaternion targetRotation = Quaternion.LookRotation(directionToPoint);
            Enemy.transform.rotation = Quaternion.Slerp(Enemy.transform.rotation, targetRotation, Time.deltaTime * 5f);
        }
    }
}