using DG.Tweening;
using UnityEngine;

public class WallRunningState : PlayerState
{
    public override float EnergyRegenRate => manager.Energy.stats.GroundedEnergyRegeneration;

    RaycastHit leftWallHit;
    RaycastHit rightWallHit;

    bool wallLeft;
    bool wallRight;

    bool jumpedOffWall;

    Tween rotateTween;

    Vector3 orient;
    public override void OnStateEnter(PlayerStateManager gamestateManager)
    {
        base.OnStateEnter(gamestateManager);
        manager.GuntipDefault();
        manager.rb.useGravity = false;
        manager.Targeting.HidePredictionPoint();

        jumpedOffWall = false;
    }

    public override void OnStateUpdate()
    {
        base.OnStateUpdate();
        manager.footstepManager.SetFootstepsEnabled(true);

        WallRunCheck();

        if (wallLeft || wallRight)
        {
            Vector3 currentWallNormal = wallRight ? rightWallHit.normal : leftWallHit.normal;
            manager.RefreshWallJumpCoyote(currentWallNormal);
        }

        if (wallLeft) rotateTween = manager.SideRotateJoint.DOLocalRotate(new Vector3(0, 0, -15f), 0.5f);
        if (wallRight) rotateTween = manager.SideRotateJoint.DOLocalRotate(new Vector3(0, 0, 15f), 0.5f);

        WallRunMovement();
        
        manager.Targeting.Predict();
        
        if (manager.Input.GrapplePressed) manager.ChangeState(manager.ThrowGrappleState);

        if (manager.Input.JumpPressed)
        {
            jumpedOffWall = true;
            manager.ChangeState(manager.BaseState);
        }
        else if (manager.Input.ForwardReleased || manager.Input.CrouchPressed)
        {
            manager.ChangeState(manager.BaseState);
        }
    }

    public override void OnStateExit()
    {
        base.OnStateExit();
        manager.footstepManager.SetFootstepsEnabled(false);
        manager.rb.useGravity = true;

        rotateTween.Kill();

        if (jumpedOffWall) WallJump();
    }
    
    private void WallJump()
    {
        Vector3 wallNormal = wallRight ? rightWallHit.normal : leftWallHit.normal;
        manager.ApplyWallJump(wallNormal);
    }

    private void WallRunMovement()
    {
        orient = new Vector3(manager.Cam.transform.forward.x, 0 , manager.Cam.transform.forward.z).normalized;

        Rigidbody rb = manager.rb;

        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);

        Vector3 wallNormal = wallRight ? rightWallHit.normal : leftWallHit.normal;

        Vector3 wallForward = Vector3.Cross(wallNormal, manager.transform.up);

        if ((orient - wallForward).magnitude > (orient - -wallForward).magnitude)
            wallForward = -wallForward;



        float currentVelocity = rb.linearVelocity.magnitude;

        if (currentVelocity > manager.locomotionStats.WallRunMaxSpeed)
        {
            float dampedSpeed = Mathf.MoveTowards(currentVelocity, manager.locomotionStats.WallRunMaxSpeed, manager.locomotionStats.WallRunOverspeedDampRate * Time.deltaTime);
            rb.linearVelocity = rb.linearVelocity.normalized * dampedSpeed;
        }
        else
        {
            float currentMoveSpeed = currentVelocity + manager.locomotionStats.WallRunAccel;
            rb.AddForce(currentMoveSpeed * wallForward.normalized * Time.deltaTime, ForceMode.VelocityChange);
        }
    }

    private void WallRunCheck()
    {
        wallRight = Physics.Raycast(manager.transform.position, manager.Cam.transform.right, out rightWallHit, manager.locomotionStats.WallCheckDistance, manager.TerrainLayer);
        wallLeft = Physics.Raycast(manager.transform.position, -manager.Cam.transform.right, out leftWallHit, manager.locomotionStats.WallCheckDistance, manager.TerrainLayer);
        bool grounded = Physics.Raycast(manager.transform.position, Vector3.down, manager.locomotionStats.GroundCheckDistance, LayerMask.GetMask("Ground"));
        
        grounded = false;//overwrite no ground can cancel out
        
        if(grounded || (!wallLeft && !wallRight))
        {
            manager.ChangeState(manager.BaseState);
        }
    }
}
