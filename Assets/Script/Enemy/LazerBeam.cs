using UnityEngine;

namespace Script.Enemy
{
    [RequireComponent(typeof(LineRenderer), typeof(CapsuleCollider))]
    public class LazerBeam : MonoBehaviour
    {
        [SerializeField] private float radius = 0.3f;
        [SerializeField] private float damageInterval = 0.5f;
        [SerializeField] private ParticleSystem startParticles;
        [SerializeField] private ParticleSystem endParticles;

        private LineRenderer line;
        private CapsuleCollider capsule;
        private float nextDamageTime;

        private void Awake()
        {
            line = GetComponent<LineRenderer>();
            capsule = GetComponent<CapsuleCollider>();
            capsule.isTrigger = true;
            capsule.direction = 2; // Z axis, matches Quaternion.LookRotation's forward below
            capsule.center = Vector3.zero;
        }
        
        public void UpdateBeam(Vector3 start, Vector3 end)
        {
            line.SetPosition(0, start);
            line.SetPosition(1, end);

            Vector3 toEnd = end - start;
            float length = toEnd.magnitude;

            transform.position = (start + end) * 0.5f;
            if (length > 0.0001f) transform.rotation = Quaternion.LookRotation(toEnd);

            capsule.height = length;
            capsule.radius = radius;

            if (startParticles != null) startParticles.transform.position = start;
            if (endParticles != null) endParticles.transform.position = end;
        }

        private void OnTriggerStay(Collider other)
        {
            if (Time.time < nextDamageTime) return;
            bool hitPlayer = (1 << other.gameObject.layer & GlobalReference.Instance.playerLayer) != 0;
            if (!hitPlayer) return;

            nextDamageTime = Time.time + damageInterval;
            GlobalReference.Instance.player.Health.takedamage();
        }
    }
}
