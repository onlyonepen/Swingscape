using UnityEngine;

public class SlideState : PlayerState
{
    Vector3 SlideDir;
    Vector3 originalCamPos; // Added to track camera height

    public override void OnStateEnter(PlayerStateManager gamestateManager)
    {
        base.OnStateEnter(gamestateManager);
        manager.PBM.playerCanMove = false;

        // Store the original camera position so we don't permanently shift it
        originalCamPos = manager.Cam.transform.localPosition;
        
        // Snap the camera down visually (adjust the 0.8f up or down to change slide depth)
        manager.Cam.transform.localPosition = new Vector3(originalCamPos.x, originalCamPos.y - 0.8f, originalCamPos.z);

        manager.GuntipDefault();
    }

    public override void OnStateUpdate()
    {
        base.OnStateUpdate();

        manager.Targeting.Predict();

        SlideLogic();

        if (manager.Input.GrapplePressed) manager.ChangeState(manager.ThrowGrappleState);
    }

    public override void OnStatePhysicsUpdate()
    {
        base.OnStatePhysicsUpdate();
    }

    public override void OnStateExit()
    {
        base.OnStateExit();

        // Safely snap the camera back up to its original height
        manager.Cam.transform.localPosition = originalCamPos; 
    }

    private void SlideLogic()
    {
        SlideDir = new Vector3(manager.GroundNormal().x, 0, manager.GroundNormal().z);

        float slopeAngle = Vector3.Angle(manager.GroundNormal(), Vector3.up);
        float speed = Physics.gravity.magnitude * slopeAngle * manager.SlideSpeedMult;

        SlideDir = Quaternion.AngleAxis(slopeAngle, manager.transform.right) * SlideDir;
        manager.rb.AddForce(speed * SlideDir);


        float strafeOffset = manager.Input.StrafeRightHeld ? 10 : (manager.Input.StrafeLeftHeld ? -10 : 0);

        float targetYRotation = manager.transform.eulerAngles.y + strafeOffset;
        Quaternion targetRotation = Quaternion.Euler(0, targetYRotation, 0);

        float verticalSpeed = manager.rb.linearVelocity.y;
        Vector3 horizontalVelocity = new Vector3(manager.rb.linearVelocity.x, 0, manager.rb.linearVelocity.z);
        float horizontalMagnitude = horizontalVelocity.magnitude;

        Vector3 newHorizontalDir = targetRotation * Vector3.forward;
        manager.rb.linearVelocity = (newHorizontalDir * horizontalMagnitude) + (Vector3.up * verticalSpeed);

        //Friction
        manager.rb.AddForce(-manager.rb.linearVelocity * (1 - manager.SlideFriction));


        if (manager.Input.CrouchReleased || manager.rb.linearVelocity.magnitude <= manager.SlideSpeedTreshold || manager.Input.JumpPressed)
        {
            if (manager.Input.JumpPressed) manager.PBM.Jump();
            manager.ChangeState(manager.BaseState);
        }
    }
}