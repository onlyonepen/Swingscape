using DG.Tweening;
using UnityEngine;

public class PlayerBaseState : PlayerState
{
    private float distanceToFeet;
    private Tween armtween;

    public override void OnStateEnter(PlayerStateManager gamestateManager)
    {
        base.OnStateEnter(gamestateManager);

        manager.GuntipDefault();

        manager.PBM.playerCanMove = true;

        distanceToFeet = Vector3.Distance(manager.transform.position, manager.feetTrans.position);

        manager.SideRotateJoint.DOLocalRotate(new Vector3(0, 0, 0f), 0.5f);

        manager.GrappleArm.localPosition = manager.initialHandPos;
        manager.GrappleArm.localRotation = manager.initialHandRot;
        
        //hardcode
        manager.grappleGun.gameObject.SetActive(false);
    }

    public override void OnStateUpdate()
    {
        base.OnStateUpdate();

        manager.Targeting.Predict();
        
        WallRunCheck();
        MantleCheck();
        SlideCheck();
        
        if(!manager.PBM.isGrounded) manager.footstepManager.SetFootstepsEnabled(false);
        else  manager.footstepManager.SetFootstepsEnabled(true);
        
        if (manager.Input.GrapplePressed && manager.Energy.UseEnergy(manager.Energy.InitialThrowUsage))
        {
            manager.grappleGun.gameObject.SetActive(true);
            manager.ChangeState(manager.ThrowGrappleState);
        }
    }

    private void WallRunCheck()
    {
        bool wallRightUpper = Physics.Raycast(manager.transform.position, manager.Cam.transform.right, manager.WallCheckDistance, manager.TerrainLayer);
        bool wallRightLower = Physics.Raycast(manager.transform.position - Vector3.up * manager.PlayerHeightOffset, manager.Cam.transform.right, manager.WallCheckDistance, manager.TerrainLayer);
        bool wallLeftUpper = Physics.Raycast(manager.transform.position, -manager.Cam.transform.right, manager.WallCheckDistance, manager.TerrainLayer);
        bool wallLeftLower = Physics.Raycast(manager.transform.position - Vector3.up * manager.PlayerHeightOffset , -manager.Cam.transform.right, manager.WallCheckDistance, manager.TerrainLayer);

        bool wallRight = wallRightLower && wallRightUpper;
        bool wallLeft = wallLeftLower && wallLeftUpper;
        
        if ((wallRight || wallLeft) && !manager.PBM.isGrounded && manager.Input.ForwardHeld)
        {
            manager.ChangeState(manager.WallRunState);
        }
    }

    private void MantleCheck()
    {
        if (manager.PBM.isGrounded) return ;
        if (!manager.Input.ForwardHeld) return;

        float forwardReach = 1.5f;
        Vector3 origin = manager.transform.position;
        Vector3 forward = manager.transform.forward;

        if (Physics.Raycast(origin + (Vector3.up * 0.5f), forward, out RaycastHit wallHit, forwardReach, manager.TerrainLayer))
        {
            bool headBlocked = Physics.Raycast(origin + (Vector3.up * manager.PlayerHeightOffset), Vector3.up, manager.PlayerHeightOffset, manager.TerrainLayer);
            if (headBlocked) return;

            Vector3 ledgeCheckOrigin = wallHit.point + (forward * 0.2f) + (Vector3.up * manager.PlayerHeightOffset);

            if (Physics.Raycast(ledgeCheckOrigin, Vector3.down, out RaycastHit ledgeHit, manager.PlayerHeightOffset, manager.TerrainLayer))
            {
                Vector3 targetPos = ledgeHit.point;

                bool spaceOccupied = Physics.CheckCapsule(targetPos + Vector3.up * 0.5f, targetPos + Vector3.up * (manager.PlayerHeightOffset - 0.5f), 0.4f, manager.TerrainLayer);

                if (!spaceOccupied)
                {
                    manager.RUD.MantlePoint = targetPos + Vector3.up * distanceToFeet;
                    manager.ChangeState(manager.MantleState);
                }
            }
        }
    }
    private void SlideCheck()
    {
        if (!manager.PBM.isGrounded) return;
        if (!manager.Input.CrouchHeld) return;

        Vector3 playerfwd = manager.transform.forward;

        Vector3 groundNormal = manager.GroundNormal();
        float angelBetween = Vector3.Angle(playerfwd, groundNormal);
        bool canSlideWithSlope = angelBetween < 85 && angelBetween > 50;
        bool canSlideFlatGround = angelBetween >= 85 && manager.rb.linearVelocity.magnitude > manager.PBM.walkSpeed;
        if (canSlideWithSlope || canSlideFlatGround)
        {
            manager.ChangeState(manager.SlideState);
        }
    }
    
    private void AirControl()
    {
        // 1. Cleanly grab and normalize inputs so diagonal movement isn't faster
        // (This fixes a small bug where your old vertInput was overriding before horiInput calculated)
        Vector2 inputDir = manager.Input.Move.normalized;
    
        float vertical = inputDir.y * manager.AirControlFwdForce;
        float horizontal = inputDir.x * manager.AirControlHorizontalForce;
    
        // 2. Calculate the base force direction using the camera's orientation
        Vector3 TotalForceDir = (manager.Cam.transform.forward * vertical) + (manager.Cam.transform.right * horizontal);

        // 3. THE FIX: Flatten the Y axis entirely so the player cannot fly upwards or downwards
        TotalForceDir.y = 0f;

        // 4. THE NERF: Tone down the overall air control
        // Tweak this float to dial in the exact "heaviness" you want in the air

        // 5. Apply the speed boost if under walking speed
        if (manager.rb.linearVelocity.magnitude < manager.PBM.walkSpeed) 
        {
            TotalForceDir *= 2f;
        }
        
        // 6. Apply the final flattened, dampened force
        manager.rb.AddForce(TotalForceDir * Time.deltaTime, ForceMode.Force);
    }
}
