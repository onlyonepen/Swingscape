using Script.Enemy;
using UnityEngine;

public class GrapplePullintoState : PlayerState
{
    private float initialEnemyDistance;
    private Vector3 initialPlayerPosition;
    private float expectedDuration;

    private bool grappleEnemy;
    private BaseEnemy enemy;

    // --- QTE finisher: a click timed to when the sprites meet (i.e. when the pull actually
    // completes) executes the enemy immediately instead of requiring a separately-aimed swing.
    // Driven off LIVE distance to the exit threshold below, not elapsed/expectedDuration —
    // expectedDuration assumes a constant closing speed, so it drifts out of sync with the
    // real meeting point if the target moves (physics, stagger knockback, etc.) during the pull.
    private const float ExitDistance = 6f;
    private const float QteTimingTolerance = 0.2f; // seconds of forgiveness, converted to distance via PullIntoSpeed
    private const float QteFacingDotThreshold = 0.5f; // ~60 degree cone — "general direction" is enough
    private bool qteAttempted;

    public override void OnStateEnter(PlayerStateManager gamestateManager)
    {
        base.OnStateEnter(gamestateManager);
        
        manager.playerHp.TurnOnInvulnerability();
        
        manager.PBM.playerCanMove = false;
        manager.PBM.FloatingCapsuleActive = false;
        if (manager.PBM.hasFallingExtraGrav)
        {
            manager.PBM.hasFallingExtraGrav = false;
        }

        manager.GrappleLr.enabled = true;
        
        initialPlayerPosition = manager.transform.position;
        initialEnemyDistance = Vector3.Distance(initialPlayerPosition, manager.RUD.GrappledObject.transform.position);
        expectedDuration = initialEnemyDistance / manager.grappleStats.PullIntoSpeed;
        
        if (manager.RUD.GrappledObject.TryGetComponent<BaseEnemy>( out var component))
        {
            component.GetPull();
            grappleEnemy = true;
            enemy = component;
        }

        qteAttempted = false;
        StaggerQTEIndicator.Instance?.Begin(manager.RUD.GrappledObject.transform, manager.Cam);

        Vector3 pullDirection = (manager.RUD.GrappledObject.transform.position - initialPlayerPosition).normalized;
        manager.rb.linearVelocity = pullDirection * manager.grappleStats.PullIntoSpeed;
    }

    public override void OnStateUpdate()
    {
        base.OnStateUpdate();

        manager.RUD.GrapplePoint = manager.RUD.GrappledObject.transform.position;
        manager.GuntipPointToGrapple();
        
        Vector3 trueTarget = manager.RUD.GrappledObject.transform.position;
        float currentOffset = manager.grappleStats.GrappleEnemyOffset;

        // Check if object is an enemy and dynamically calculate offset from collider size
        if (Grappleable.Resolve(manager.RUD.GrappledObject) != GrappleType.Normal)
        {
            if (manager.RUD.GrappledObject.TryGetComponent<Collider>(out Collider col))
            {
                currentOffset = Mathf.Max(col.bounds.extents.x, col.bounds.extents.z);
            }
        }

        Vector3 dirToPlayer = (manager.transform.position - trueTarget).normalized;
        Vector3 visualPos = trueTarget + (dirToPlayer * currentOffset);
        
        float elapsed = Time.time - stateEnterTime;
        float percent = expectedDuration > 0 ? Mathf.Clamp01(elapsed / expectedDuration) : 1f;
        
        PullIn(percent);

        if (percent >= 0.95f)
        {
            if (manager.RUD.GrappledObject.TryGetComponent<ISliceable>(out var component))
            {
                //component.SplitDeath();
            }
        }

        manager.GrappleLr.SetPosition(0, manager.Guntip.position);
        manager.GrappleLr.SetPosition(1, visualPos);

        float distance = Vector3.Distance(manager.transform.position, trueTarget);

        // Progress/hit-window both key off this same live distance, not expectedDuration,
        // so they always match the instant the exit check below actually fires.
        float meetProgress = Mathf.Clamp01(1f - (distance - ExitDistance) / Mathf.Max(initialEnemyDistance - ExitDistance, 0.0001f));
        StaggerQTEIndicator.Instance?.SetProgress(meetProgress);
        TryResolveQte(distance);

        if (distance <= ExitDistance)
        {
            manager.ChangeState(manager.BaseState);
        }
    }

    public override void OnStateExit()
    {
        base.OnStateExit();
        
        manager.playerHp.TurnOffInvulnerability();

        manager.GrappleLr.enabled = false;
        manager.GrappleLr.positionCount = 2;

        StaggerQTEIndicator.Instance?.Cancel();

        manager.PBM.FloatingCapsuleActive = true;

        Vector3 pullDirection = (manager.RUD.GrappledObject.transform.position - initialPlayerPosition).normalized;
        manager.rb.linearVelocity = pullDirection * manager.grappleStats.PullIntoSpeed * 0.8f;
        
        //if(grappleEnemy) enemy.SplitDeath();
    }

    /// <summary>One attempt per pull: a click is judged the instant it's pressed, hit or miss,
    /// against how close the LIVE distance is to ExitDistance — the actual threshold that ends
    /// the pull — converted from the requested 0.2s of forgiveness via the known pull speed.</summary>
    private void TryResolveQte(float currentDistance)
    {
        if (qteAttempted || !grappleEnemy) return;
        if (!manager.Input.AttackPressed) return;

        qteAttempted = true;

        float distanceTolerance = QteTimingTolerance * manager.grappleStats.PullIntoSpeed;
        bool inTimingWindow = Mathf.Abs(currentDistance - ExitDistance) <= distanceTolerance;

        GameObject target = manager.RUD.GrappledObject;
        Vector3 dirToEnemy = (target.transform.position - manager.Cam.transform.position).normalized;
        bool isFacingEnemy = Vector3.Dot(manager.Cam.transform.forward, dirToEnemy) >= QteFacingDotThreshold;

        if (inTimingWindow && isFacingEnemy)
        {
            manager.Manager.Combat.ExecuteFinisherHit(target);
        }
    }

    private void PullIn(float percent)
    {
        Vector3 origin = initialPlayerPosition;
        Vector3 targetGoal = manager.RUD.GrappledObject.transform.position; 
        
        Vector3 newPos = Vector3.Lerp(origin, targetGoal, percent);
        manager.rb.MovePosition(newPos); 
    }
}