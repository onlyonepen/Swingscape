using UnityEngine;

namespace Script.Enemy.State.Attack
{
    public class DroneLaserAttack : EnemyBaseState
    {
        private readonly HeavyDroneConfig config;
        private LazerBeam beam;
        private Vector3 trackedEnd;

        // The beam reads LazerStartPoint.position every frame; bobbing would move the head after
        // that read, leaving the beam permanently a frame behind the visual bounce.
        public override bool AllowsBob => false;

        public DroneLaserAttack(BaseEnemy enemy)
        {
            Enemy = enemy;
            config = enemy.GetComponent<HeavyDroneConfig>();
        }

        public override void OnStateEnter()
        {
            base.OnStateEnter();
            Enemy.rb.constraints = RigidbodyConstraints.FreezeAll;
            Enemy.ChargeUpParticles.Play();

            // The endpoint is "created" here and chases the player for the whole anticipation
            // window, so it's already mid-track (not snapping into place) once the beam fires.
            trackedEnd = GlobalReference.Instance?.player != null
                ? GlobalReference.Instance.player.transform.position
                : Enemy.transform.position;
        }

        public override void OnStateUpdate()
        {
            base.OnStateUpdate();

            if (GlobalReference.Instance?.player == null)
            {
                Enemy.ChangeState(Enemy.stateFactory.CreateIdleState(Enemy));
                return;
            }

            // Base speed plus a distance-scaled bonus, so the farther the endpoint is from the
            // player, the faster it closes the gap.
            Vector3 playerPos = GlobalReference.Instance.player.transform.position;
            float distanceToPlayer = Vector3.Distance(trackedEnd, playerPos);
            float trackSpeed = config.LazerTrackBaseSpeed + distanceToPlayer * config.LazerTrackSpeedDistanceMult;
            trackedEnd = Vector3.MoveTowards(trackedEnd, playerPos, trackSpeed * Time.deltaTime);

            LookAtEndpoint();

            float elapsed = Time.time - StateEnterTime;
            if (elapsed < config.LazerAnticipation) return;

            if (beam == null)
            {
                beam = config.GetLazerBeam();
                beam.gameObject.SetActive(true);
            }

            Vector3 start = config.LazerStartPoint.position;
            Vector3 direction = (trackedEnd - start).normalized;
            Vector3 beamEnd = start + direction * config.LazerMaxDistance;

            // The beam passes through the player (damage comes from the beam's own trigger, not
            // this raycast) and only actually stops when it hits the environment. TerrainLayer
            // alone is just ground - use everything-but-player (same obstacle mask DroneAggro/
            // DroneIdle use) so walls and other obstacles block it too.
            LayerMask obstacleMask = ~GlobalReference.Instance.playerLayer;
            if (Physics.Raycast(start, direction, out RaycastHit hit, config.LazerMaxDistance, obstacleMask))
            {
                beamEnd = hit.point;
            }

            beam.UpdateBeam(start, beamEnd);

            if (elapsed > config.LazerAnticipation + config.LazerDuration)
            {
                Enemy.ChangeState(Enemy.stateFactory.CreateAggroState(Enemy));
            }
        }

        public override void OnStateExit()
        {
            Enemy.ChargeUpParticles.Stop();
            if (beam != null) beam.gameObject.SetActive(false);
        }

        private void LookAtEndpoint()
        {
            Vector3 dronePos = Enemy.transform.position;
            Vector3 directionToEndpoint = (trackedEnd - dronePos).normalized;

            Quaternion targetRotation = Quaternion.LookRotation(directionToEndpoint);
            Enemy.transform.rotation = Quaternion.Slerp(Enemy.transform.rotation, targetRotation, Time.deltaTime * 5f);
        }
    }
}
