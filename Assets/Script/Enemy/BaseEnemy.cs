using System;
using System.Collections;
using System.Collections.Generic;
using JL.Splitting;
using Script.Enemy.EnemiesStats;
using Script.Enemy.State;
using UnityEngine;
using VInspector;

namespace Script.Enemy
{
    [RequireComponent(typeof(Grappleable), typeof(Attackable))]
    public class BaseEnemy : MonoBehaviour, ISliceable
    {
        public EnemyType Type;
        [SerializeField] internal EnemyStatSO Stat;
        [SerializeField] internal Rigidbody rb;
        [SerializeField] private ParticleSystem DeathParticles;
        [SerializeField] internal Transform Guntip;
        [SerializeField] internal GameObject ProjectilePrefab;
        [SerializeField] internal ParticleSystem ChargeUpParticles;

        /// <summary>Fired by any enemy the moment it dies. Subscribe to reward the player, update score, etc.</summary>
        public static event Action OnAnyEnemyDied;

        public IEnemyStateFactory stateFactory { get; private set; }
        private EnemyBaseState currentState;

        [HideInInspector] public bool Iskilled = false;

        private Collider col;
        private Attackable attackable;

        private void Awake()
        {
            GetComponent<Grappleable>().Type = Type == EnemyType.HeavyDrone ? GrappleType.Heavy : GrappleType.Light;
            attackable = GetComponent<Attackable>();
        }

        private void Start()
        {
            stateFactory = CreateFactory(Type);
            currentState = stateFactory.CreateIdleState(this);
            currentState.OnStateEnter();
            
            col =  GetComponent<Collider>();
        }

        public void ChangeState(EnemyBaseState nextState)
        {
            currentState.OnStateExit();
            currentState = nextState;
            currentState.OnStateEnter();
        }

        
        private void Update()
        {
            currentState.OnStateUpdate();
        }

        public void GetPull()
        {
            ChangeState(stateFactory.CreateStaggerState(this));
            attackable.TriggerHitEffects();
        }
        
        public void OnSliceStart()
        {
            // Make the enemy untargetable immediately so the player can't hit it again.
            // (This ensures our new OverlapBox cast won't catch it while it's asynchronously splitting)
            if (TryGetComponent<Collider>(out var col))
            {
                col.enabled = false;
            }

            // Use the terminal Dead state (not Stagger) so the enemy can't wake back up into
            // Aggro/Idle mid-air while the async split is still running.
            ChangeState(stateFactory.CreateDeadState(this));
        }

        private GameObject slicedPosObject;
        private GameObject slicedNegObject;

        public void OnSliceComplete(SplitResult result)
        {
            slicedPosObject = result.posObject;
            slicedNegObject = result.negObject;
            StartCoroutine(WaitAndDie());
        }

        // Called once by DroneDead.OnStateEnter — i.e. the moment the enemy first enters the
        // death state, not when Death() finishes tidying up frames later.
        public void PlayDeathParticles()
        {
            if (DeathParticles == null) return;
            DeathParticles.gameObject.SetActive(false);
            DeathParticles.gameObject.SetActive(true);
            DeathParticles.gameObject.transform.parent = null;
            DeathParticles.Play();
        }

        public void Death()
        {
            OnAnyEnemyDied?.Invoke();

            // The split halves get reparented to the scene root when they're cut (so they can
            // fly apart with physics), so they're no longer children of this object by the time
            // it deactivates below — clean them up after a delay (instead of instantly) so the
            // debris is actually visible flying apart rather than vanishing with the enemy.
            const float debrisLifetime = 3f;
            if (slicedPosObject != null) Destroy(slicedPosObject, debrisLifetime);
            if (slicedNegObject != null) Destroy(slicedNegObject, debrisLifetime);

            gameObject.SetActive(false);
        }


        // To add a new enemy type: add its EnemyType value to the enum, create a factory class,
        // then register it here. BaseEnemy itself never needs to change again.
        private static readonly Dictionary<EnemyType, Func<IEnemyStateFactory>> FactoryRegistry =
            new Dictionary<EnemyType, Func<IEnemyStateFactory>>
            {
                { EnemyType.LightDrone, () => new LightDroneFactory() },
                { EnemyType.HeavyDrone, () => new HeavyDroneFactory() },
            };

        private IEnemyStateFactory CreateFactory(EnemyType type)
        {
            if (FactoryRegistry.TryGetValue(type, out Func<IEnemyStateFactory> create))
                return create();
            throw new NotImplementedException("No factory registered for enemy type: " + type);
        }

        internal float staggerTime;
        public void Stagger(float time = 120)
        {
            staggerTime = time;
            ChangeState(stateFactory.CreateStaggerState(this));
        }
        private IEnumerator WaitAndDie()
        {
            // The Splittable plugin requires exactly 1 frame to run its "ResetCenterOfMassNextFrame" coroutine.
            // We yield twice just to be absolutely safe before disabling the parent GameObject.
            yield return null;
            yield return null;
    
            Death();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, Stat.DetectionRange);
        }
    }
}