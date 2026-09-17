using Script.Enemy;
using UnityEngine;

public class BasicEnemyProjectile : MonoBehaviour
{
        public float speed = 50f;
        public float lifetime = 10f;
        private float spawnTimeStamp;

        [HideInInspector]public BaseEnemy ProjectileOwner;

        private void OnEnable()
        {
                spawnTimeStamp = Time.time;
        }

        private void Update()
        {
                transform.position += transform.forward * speed * Time.deltaTime;
                if (Time.time - spawnTimeStamp > lifetime)
                {
                        Destroy(gameObject);
                }
        }

        private void OnCollisionEnter(Collision collision)
        {
                bool hitPlayer = (1 << collision.gameObject.layer & GlobalReference.Instance.playerLayer) != 0;
                bool hitTerrain = (1 << collision.gameObject.layer & GlobalReference.Instance.TerrainLayer) != 0;
                if(hitPlayer) GlobalReference.Instance.player.Health.takedamage();
                if(hitPlayer || hitTerrain) Destroy(gameObject);
        }
}