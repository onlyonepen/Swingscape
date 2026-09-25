using DG.Tweening;
using System.Collections;
using Script.Enemy;
using UnityEngine;

public class GrapplePullState : PlayerState
{
    private float airFloatForce = 7f;
    private SpringJoint joint;
    
    private float initialEnemyDistance;
    private Vector3 initialPlayerPosition;
    private float expectedDuration;

    GameObject grappledObj;
    private Rigidbody grappledObjRb;
    Vector3 initialObjPos;

    bool grappleEnemy = false;
    private BaseEnemy enemy;

    public override void OnStateEnter(PlayerStateManager gamestateManager)
    {
        base.OnStateEnter(gamestateManager);
        
        manager.playerHp.TurnOnInvulnerability();

        grappledObj = manager.RUD.GrappledObject;
        
        // Safety check: If the object is null OR deactivated right as we enter, abort immediately.
        if (grappledObj == null || !grappledObj.activeInHierarchy)
        {
            manager.ChangeState(manager.BaseState);
            return;
        }

        initialObjPos = grappledObj.transform.position;
        
        grappledObj.TryGetComponent<Rigidbody>(out grappledObjRb);

        if (grappledObjRb != null)
        {
            // Clear any constraints set in the inspector (e.g. FreezePosition to keep the
            // object stable pre-grapple) so MovePosition below can actually move it.
            grappledObjRb.constraints = RigidbodyConstraints.None;
        }

        initialPlayerPosition = manager.transform.position;
        initialEnemyDistance = Vector3.Distance(initialPlayerPosition, manager.RUD.GrappledObject.transform.position);
        expectedDuration = initialEnemyDistance / 40;

        if (grappledObj.TryGetComponent<BaseEnemy>( out var component))
        {
            component.GetPull();
            grappleEnemy = true;
            enemy = component;
        }

        // Ensure the line renderer is ready for a simple 2-point straight line
        manager.GrappleLr.enabled = true;
        manager.GrappleLr.positionCount = 2;
    }

    public override void OnStateUpdate()
    {
        base.OnStateUpdate();

        // 1. THE EARLY EXIT GUARD (Updated for Object Pooling)
        // If the enemy was deactivated (SetActive(false)) this frame, abort the pull instantly.
        if (grappledObj == null || !grappledObj.activeInHierarchy)
        {
            manager.ChangeState(manager.BaseState);
            return; 
        }

        manager.RUD.GrapplePoint = grappledObj.transform.position;
        manager.GuntipPointToGrapple();

        float currentOffset = manager.grappleStats.GrappleEnemyOffset;

        // Check if object is an enemy and dynamically calculate offset from collider size
        if (Grappleable.Resolve(grappledObj) != GrappleType.Normal)
        {
            if (grappledObj.TryGetComponent<Collider>(out Collider col))
            {
                currentOffset = Mathf.Max(col.bounds.extents.x, col.bounds.extents.z);
            }
        }

        Vector3 dirToPlayer = (manager.transform.position - grappledObj.transform.position).normalized;
        Vector3 visualPos = grappledObj.transform.position + (dirToPlayer * currentOffset);
        
        // Draw simple straight rope
        manager.GrappleLr.SetPosition(0, manager.Guntip.position);
        manager.GrappleLr.SetPosition(1, visualPos);

        float elapsed = Time.time - stateEnterTime;
        float percent = expectedDuration > 0 ? Mathf.Clamp01(elapsed / expectedDuration) : 1f;
        
        UpdateObjectPos(percent);
        
        // 2. Secondary active check just in case UpdateObjectPos triggered a deactivation
        if (grappledObj != null && grappledObj.activeInHierarchy) 
        {
            float distance = Vector3.Distance(manager.transform.position, grappledObj.transform.position);
            if (distance <= 3f) 
            {
                manager.ChangeState(manager.BaseState);
            }
        }
        
        manager.rb.AddForce(Vector3.up * airFloatForce, ForceMode.Acceleration);
    }

    public override void OnStateExit()
    {
        base.OnStateExit();
        
        manager.playerHp.TurnOffInvulnerability();
        
        manager.GrappleLr.enabled = false;
        
        if (grappledObjRb != null) 
        {
            grappledObjRb.linearVelocity = Vector3.zero;
        }
    }

    private void UpdateObjectPos(float percent)
    {
        Vector3 origin = manager.Cam.transform.position + (manager.Cam.transform.forward * 1);
        Vector3 target = initialObjPos;
        Vector3 pos = Vector3.Lerp(target, origin, percent);
        
        if (grappledObjRb != null)
        {
            grappledObjRb.MovePosition(pos);
        }
        else if (grappledObj != null && grappledObj.activeInHierarchy) 
        {
            grappledObj.transform.position = pos;
        }
        
        if (grappledObj != null && grappledObj.activeInHierarchy) 
        {
            float distance = Vector3.Distance(pos, manager.transform.position);
            if (distance <= 1) 
            {
                manager.ChangeState(manager.BaseState);
            }
        }
    }
}