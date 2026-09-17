using System;
using System.Collections;
using System.Collections.Generic;
using Script.Enemy;
using UnityEngine;

public class PlayerAttacking : MonoBehaviour
{
    private enum AttackState
    {
        Idle,
        Attack1,
        Attack2
    }
    
    [SerializeField] private PlayerAttackArea attackArea;
    [SerializeField] private AttackState currentAttackState = AttackState.Idle;
    [SerializeField] private Animator armAnimator; 
    
    [SerializeField] private Transform attack1Plane;
    [SerializeField] private Transform attack2Plane;
    

    // --- NEW: Timing Delays ---
    [Header("Impact Timings")]
    [Tooltip("Time in seconds before the Attack 1 hitbox is active")]
    [SerializeField] private float attack1HitDelay = 0.2f; 
    [Tooltip("Time in seconds before the Attack 2 hitbox is active")]
    [SerializeField] private float attack2HitDelay = 0.25f;
    [Tooltip("Time scale held while a melee hit connects (impact slow-mo)")]
    [SerializeField] private float attackSlowMoScale = 0.5f;

    private bool nextAttackQueued = false;

    private PlayerManager manager;
    private int parriableLayer;
    private LayerMask splitAndDamagable;

    /// <summary>Active weapon mode. Null = the built-in melee combo in this class.
    /// Assign via EquipMode() when you add real weapon modes later; the primary-attack
    /// input then routes to the mode instead of the built-in melee.</summary>
    private IWeaponMode currentMode;

    private void Awake()
    {
        manager = GetComponentInParent<PlayerManager>();
    }

    private void Start()
    {
        parriableLayer    = LayerMask.NameToLayer("Parriable");
        splitAndDamagable = LayerMask.GetMask("SplittableObject") | GlobalReference.Instance.EnemyLayer;
    }

    /// <summary>Swap the active weapon mode. Pass null to fall back to built-in melee.</summary>
    public void EquipMode(IWeaponMode mode)
    {
        currentMode?.OnUnequip();
        currentMode = mode;
        currentMode?.OnEquip(this);
    }

    private void Update()
    {
        currentMode?.Tick();

        if (manager.Input.AttackPressed)
        {
            if (currentMode != null) currentMode.OnPrimaryPressed();
            else HandlePrimaryAttack();
        }
    }

    // ---- Built-in melee weapon (default until an IWeaponMode is equipped) ----

    private void HandlePrimaryAttack()
    {
        if (currentAttackState == AttackState.Idle)
        {
            StartCombo();
        }
        else if (currentAttackState == AttackState.Attack1)
        {
            nextAttackQueued = true;
        }
    }

    private void StartCombo()
    {
        currentAttackState = AttackState.Attack1;
        nextAttackQueued = false;

        armAnimator.Play("Attack1");
        
        // Fire the delayed attack execution instead of instantaneous
        StartCoroutine(DelayedExecuteAttack(attack1Plane, attack1HitDelay));
        
        StartCoroutine(WaitAndTransition(CheckCombo));
    }

    private void CheckCombo()
    {
        if (nextAttackQueued)
        {
            HitStopUtil.Instance.ResetBaseTimeScale();
            currentAttackState = AttackState.Attack2;
            nextAttackQueued = false;

            armAnimator.Play("Attack2"); 

            // Fire the delayed attack execution for the second swing
            StartCoroutine(DelayedExecuteAttack(attack2Plane, attack2HitDelay));
            
            StartCoroutine(WaitAndTransition(BackToIdle));
        }
        else
        {
            HitStopUtil.Instance.ResetBaseTimeScale();
            BackToIdle();
        }
    }

    private void BackToIdle()
    {
        currentAttackState = AttackState.Idle;
        nextAttackQueued = false;
        
        armAnimator.Play("Idle");
    }

    private IEnumerator WaitAndTransition(Action nextStateMethod)
    {
        yield return null; 
        
        float currentAnimLength = armAnimator.GetCurrentAnimatorStateInfo(0).length;
        
        yield return new WaitForSeconds(currentAnimLength);
        
        nextStateMethod.Invoke();
    }
    // --- UPDATED: Continuous Scanning & Caching Coroutine ---
    private IEnumerator DelayedExecuteAttack(Transform activePlane, float delayTime)
    {
        float timer = 0f;
        bool slowMoTriggered = false;
        
        // NEW: The Cache. We will store targets here the exact moment we see them.
        HashSet<GameObject> lockedTargets = new HashSet<GameObject>();

        // Loop every frame until our visual wind-up delay is reached
        string audioToPlay = "Melee";
        while (timer < delayTime)
        {
            if (attackArea != null)
            {
                GameObject[] earlyTargets = attackArea.GetTargetsInSwing();
                
                if (earlyTargets != null && earlyTargets.Length > 0)
                {
                    // The moment an enemy gets pulled into the hitbox, drop time!
                    if (!slowMoTriggered)
                    {
                        HitStopUtil.Instance.SetBaseTimeScale(attackSlowMoScale);
                        slowMoTriggered = true;
                    }
                    
                    // Lock them in! Even if you slide past them before the swing finishes, they are marked for the cut.
                    foreach (GameObject target in earlyTargets)
                    {
                        if (target != null) lockedTargets.Add(target);
                    }
                }
            }
            
            if (slowMoTriggered) audioToPlay = "MeleeHit";

            timer += Time.deltaTime;
            yield return null; 
        }
        AudioManager.Instance.PlayAudioByName(audioToPlay, transform.position, true);
        
        // ONE FINAL CHECK: Catch anyone who entered the hitbox on the exact execution frame
        if (attackArea != null)
        {
            GameObject[] finalTargets = attackArea.GetTargetsInSwing();
            if (finalTargets != null && finalTargets.Length > 0)
            {
                foreach (GameObject target in finalTargets)
                {
                    if (target != null) lockedTargets.Add(target);
                }
                
                // If we somehow hit someone without triggering slow-mo yet, trigger it now for the impact!
                if (!slowMoTriggered) HitStopUtil.Instance.SetBaseTimeScale(attackSlowMoScale);
            }
        }

        // The wind-up is over. Pass the locked targets to the hitbox logic!
        ExecuteHitboxLogic(activePlane, lockedTargets);
        
        yield return new WaitForSecondsRealtime(0.05f); // Use realtime so the pause is consistent
        HitStopUtil.Instance.ResetBaseTimeScale();
    }

    // --- UPDATED: Now receives the locked targets ---
    private void ExecuteHitboxLogic(Transform activePlane, HashSet<GameObject> targetsToProcess)
    {
        // If the enemy dodged before the radar even caught them, snap time back to normal
        if (targetsToProcess == null || targetsToProcess.Count == 0)
        {
            HitStopUtil.Instance.ResetBaseTimeScale();
            return;
        }

        // Keep this internal HashSet to prevent multiple child colliders on the SAME enemy from triggering multiple cuts
        HashSet<IDamagable> hitTargets = new HashSet<IDamagable>();
        
        foreach (GameObject obj in targetsToProcess)
        {
            if (obj == null) continue; 
    
            if (((1 << obj.layer) & splitAndDamagable) != 0)
            {
                var damagable = obj.GetComponentInParent<IDamagable>();

                if (damagable != null && hitTargets.Add(damagable))
                {
                    damagable.SplitDeath(activePlane);
                }
            }
            else if (((1 << obj.layer) & parriableLayer) != 0)
            {
                if (obj.TryGetComponent(out IParriable parriable))
                {
                    parriable.Parried();
                }
            }
        }
    }
}