using DG.Tweening;
using UnityEngine;

public class WallRunningState : PlayerState
{
    public override float EnergyRegenRate => manager.Energy.GroundedEnergyRegeneration;

    RaycastHit leftWallHit;
    RaycastHit rightWallHit;

    bool wallLeft;
    bool wallRight;

    bool upwardsRunning;
    bool downwardsRunning;

    Tween rotateTween;

    Vector3 orient;
    public override void OnStateEnter(PlayerStateManager gamestateManager)
    {
        base.OnStateEnter(gamestateManager);
        manager.GuntipDefault();
        manager.rb.useGravity = false;
        manager.Targeting.HidePredictionPoint();
    }

    public override void OnStateUpdate()
    {
        base.OnStateUpdate();
        manager.footstepManager.SetFootstepsEnabled(true);

        WallRunCheck();
        if (wallLeft) rotateTween = manager.SideRotateJoint.DOLocalRotate(new Vector3(0, 0, -15f), 0.5f);
        if (wallRight) rotateTween = manager.SideRotateJoint.DOLocalRotate(new Vector3(0, 0, 15f), 0.5f);

        WallRunMovement();
        
        manager.Targeting.Predict();
        
        if (manager.Input.GrapplePressed) manager.ChangeState(manager.ThrowGrappleState);

        if (manager.Input.JumpPressed || manager.Input.ForwardReleased) manager.ChangeState(manager.BaseState);
    }

    public override void OnStateExit()
    {
        base.OnStateExit();
        manager.footstepManager.SetFootstepsEnabled(false);
        manager.rb.useGravity = true;

        rotateTween.Kill();

        WallJump();
    }
    
    private void WallJump()
    {
        Vector3 wallNormal = wallRight ? rightWallHit.normal : leftWallHit.normal;

        Vector3 jumpDir = (wallNormal.normalized * manager.WallJumpForce) + (manager.transform.up.normalized * manager.PBM.jumpPower);

        manager.rb.AddForce(jumpDir, ForceMode.Impulse);
    }

    private void WallRunMovement()
    {
        orient = new Vector3(manager.Cam.transform.forward.x, 0 , manager.Cam.transform.forward.z).normalized;

        upwardsRunning = manager.Input.SprintHeld;
        downwardsRunning = manager.Input.CrouchHeld;

        Rigidbody rb = manager.rb;

        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);

        Vector3 wallNormal = wallRight ? rightWallHit.normal : leftWallHit.normal;

        Vector3 wallForward = Vector3.Cross(wallNormal, manager.transform.up);

        if ((orient - wallForward).magnitude > (orient - -wallForward).magnitude)
            wallForward = -wallForward;



        Vector3 targetVelocity = manager.transform.TransformDirection(orient) * manager.WallRunMaxSpeed;

        float currentVelocity = rb.linearVelocity.magnitude;
        float currentMoveSpeed = currentVelocity;

        if (currentVelocity <= manager.WallRunMaxSpeed)
        {
            currentMoveSpeed += manager.WallRunAccel;
        }
        else currentMoveSpeed = manager.WallRunMaxSpeed;

            rb.AddForce(currentMoveSpeed * wallForward.normalized * Time.deltaTime, ForceMode.VelocityChange);

        if (upwardsRunning)
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, manager.WallClimbSpeed, rb.linearVelocity.z);
        if (downwardsRunning)
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, -manager.WallClimbSpeed, rb.linearVelocity.z);
    }

    private void WallRunCheck()
    {
        wallRight = Physics.Raycast(manager.transform.position, manager.Cam.transform.right, out rightWallHit, manager.WallCheckDistance, manager.TerrainLayer);
        wallLeft = Physics.Raycast(manager.transform.position, -manager.Cam.transform.right, out leftWallHit, manager.WallCheckDistance, manager.TerrainLayer);
        bool grounded = Physics.Raycast(manager.transform.position, Vector3.down, manager.GroundCheckDistance, LayerMask.GetMask("Ground"));
        
        grounded = false;//overwrite no ground can cancel out
        
        if(grounded || (!wallLeft && !wallRight))
        {
            manager.ChangeState(manager.BaseState);
        }
    }
}
