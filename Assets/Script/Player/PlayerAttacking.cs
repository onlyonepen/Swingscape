using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerAttacking : MonoBehaviour
{
    [SerializeField] private PlayerAttackArea attackArea;
    [SerializeField] private PlayerAttackState currentPlayerAttackState = PlayerAttackState.Idle;
    [SerializeField] private Animator armAnimator; 
    
    [SerializeField] private Transform attack1Plane;
    [SerializeField] private Transform attack2Plane;
    

    // --- NEW: Timing Delays ---
    [Header("Impact Timings")]
    [Tooltip("Time in seconds before the Attack 1 hitbox is active")]
    [SerializeField] private float attack1HitDelay = 0.2f; 
    [Tooltip("Time in seconds before the Attack 2 hitbox is active")]
    [SerializeField] private float attack2HitDelay = 0.25f;

    private bool nextAttackQueued = false;

    private PlayerManager manager;

    /// <summary>Active weapon mode. Null = the built-in melee combo in this class.
    /// Assign via EquipMode() when you add real weapon modes later; the primary-attack
    /// input then routes to the mode instead of the built-in melee.</summary>
    private IWeaponMode currentMode;

    private void Awake()
    {
        manager = GetComponentInParent<PlayerManager>();
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
        if (currentPlayerAttackState == PlayerAttackState.Idle)
        {
            StartCombo();
        }
        else if (currentPlayerAttackState == PlayerAttackState.Attack1)
        {
            nextAttackQueued = true;
        }
    }

    private void StartCombo()
    {
        currentPlayerAttackState = PlayerAttackState.Attack1;
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
            TimeEffects.ResetBaseTimeScale();
            currentPlayerAttackState = PlayerAttackState.Attack2;
            nextAttackQueued = false;

            armAnimator.Play("Attack2"); 

            // Fire the delayed attack execution for the second swing
            StartCoroutine(DelayedExecuteAttack(attack2Plane, attack2HitDelay));
            
            StartCoroutine(WaitAndTransition(BackToIdle));
        }
        else
        {
            TimeEffects.ResetBaseTimeScale();
            BackToIdle();
        }
    }

    private void BackToIdle()
    {
        currentPlayerAttackState = PlayerAttackState.Idle;
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
        bool anyTargetLocked = false;

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
                    // Lock them in! Even if you slide past them before the swing finishes, they are marked for the cut.
                    foreach (GameObject target in earlyTargets)
                    {
                        if (target == null || !lockedTargets.Add(target)) continue;

                        anyTargetLocked = true;
                    }
                }
            }

            if (anyTargetLocked) audioToPlay = "MeleeHit";

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
                    lockedTargets.Add(target);
                }
            }
        }

        // The wind-up is over. Pass the locked targets to the hitbox logic!
        ExecuteHitboxLogic(activePlane, lockedTargets);
    }

    // --- UPDATED: Now receives the locked targets ---
    private void ExecuteHitboxLogic(Transform activePlane, HashSet<GameObject> targetsToProcess)
    {
        // If the enemy dodged before the radar even caught them, snap time back to normal
        if (targetsToProcess == null || targetsToProcess.Count == 0)
        {
            TimeEffects.ResetBaseTimeScale();
            return;
        }

        // Keep these internal HashSets to prevent multiple child colliders on the SAME target from triggering multiple hits
        HashSet<IDamagable> hitTargets = new HashSet<IDamagable>();
        HashSet<Rigidbody> knockedBack = new HashSet<Rigidbody>();
        HashSet<Attackable> effectsFired = new HashSet<Attackable>();

        foreach (GameObject obj in targetsToProcess)
        {
            if (obj == null) continue;

            var attackable = obj.GetComponentInParent<Attackable>();
            if (attackable == null) continue;

            // Fire this target's hit effects (slow-mo, hit-stop, ...) exactly when the attack lands.
            if (effectsFired.Add(attackable)) attackable.TriggerHitEffects();

            var ctx = new AttackContext(obj, manager.transform.position, activePlane, hitTargets, knockedBack);
            attackable.ApplyAttack(ctx);
        }
    }
}