using UnityEngine;

namespace Script.Enemy
{
    /// <summary>HeavyDrone-only tuning/references. Attach to the HeavyDrone prefab only.</summary>
    public class HeavyDroneConfig : MonoBehaviour
    {
        [Tooltip("Beam prefab (LineRenderer + CapsuleCollider) instantiated for the laser attack.")]
        public GameObject LazerPrefab;

        [Tooltip("Where the laser beam originates from.")]
        public Transform LazerStartPoint;

        [Tooltip("Seconds between the endpoint appearing and the beam actually firing.")]
        public float LazerAnticipation = 1f;

        [Tooltip("Seconds the beam stays active once it fires.")]
        public float LazerDuration = 3f;

        [Tooltip("Flat units/sec the endpoint always closes the gap to the player by.")]
        public float LazerTrackBaseSpeed = 2f;

        [Tooltip("Extra units/sec of tracking speed added per unit of distance to the player - makes the endpoint catch up faster the farther away it is.")]
        public float LazerTrackSpeedDistanceMult = 0.5f;

        [Tooltip("Max beam range if it doesn't hit anything.")]
        public float LazerMaxDistance = 100f;

        private LazerBeam lazerBeam;

        /// <summary>Lazily instantiates the beam once (inactive) and reuses it on every later attack via SetActive.</summary>
        public LazerBeam GetLazerBeam()
        {
            if (lazerBeam == null)
            {
                GameObject instance = Instantiate(LazerPrefab);
                instance.SetActive(false);
                lazerBeam = instance.GetComponent<LazerBeam>();
            }
            return lazerBeam;
        }
    }
}
