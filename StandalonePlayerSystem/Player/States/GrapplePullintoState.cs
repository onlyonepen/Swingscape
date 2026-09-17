using Script.Enemy;
using UnityEngine;

public class GrapplePullintoState : PlayerState
{
    private float initialEnemyDistance;
    private Vector3 initialPlayerPosition;
    private float expectedDuration;

    private bool grappleEnemy;
    private IGrappleInteractable enemy;
    
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
        expectedDuration = initialEnemyDistance / manager.PullIntoSpeed;
        
        if (manager.RUD.GrappledObject.TryGetComponent<IGrappleInteractable>( out var component))
        {
            component.GetPull();
            grappleEnemy = true;
            enemy = component;
        }
        

        Vector3 pullDirection = (manager.RUD.GrappledObject.transform.position - initialPlayerPosition).normalized;
        manager.rb.linearVelocity = pullDirection * manager.PullIntoSpeed;
    }

    public override void OnStateUpdate()
    {
        base.OnStateUpdate();

        manager.RUD.GrapplePoint = manager.RUD.GrappledObject.transform.position;
        manager.GuntipPointToGrapple();
        
        Vector3 trueTarget = manager.RUD.GrappledObject.transform.position;
        float currentOffset = manager.GrappleEnemyOffset;

        // Check if object is an enemy and dynamically calculate offset from collider size
        if (((1 << manager.RUD.GrappledObject.layer) & GlobalReference.Instance.EnemyLayer) != 0)
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
            if (manager.RUD.GrappledObject.TryGetComponent<IDamagable>(out var component)) 
            {
                //component.SplitDeath();
            }
        }

        manager.GrappleLr.SetPosition(0, manager.Guntip.position);
        manager.GrappleLr.SetPosition(1, visualPos); 
        
        float distance = Vector3.Distance(manager.transform.position, trueTarget);
        if (distance <= 6f) 
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

        manager.PBM.FloatingCapsuleActive = true;

        Vector3 pullDirection = (manager.RUD.GrappledObject.transform.position - initialPlayerPosition).normalized;
        manager.rb.linearVelocity = pullDirection * manager.PullIntoSpeed * 0.8f;
        
        //if(grappleEnemy) enemy.SplitDeath();
    }

    private void PullIn(float percent)
    {
        Vector3 origin = initialPlayerPosition;
        Vector3 targetGoal = manager.RUD.GrappledObject.transform.position; 
        
        Vector3 newPos = Vector3.Lerp(origin, targetGoal, percent);
        manager.rb.MovePosition(newPos); 
    }
}