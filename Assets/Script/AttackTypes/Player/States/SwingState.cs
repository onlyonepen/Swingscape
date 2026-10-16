using UnityEngine;

public class SwingState : PlayerState
{
    public override float EnergyRegenRate => 0f;

    private GrappleType grapple;
    
    private Vector3 GrapplePoint;
    private SpringJoint joint;
    private float currentDist;

    private float vertInput;
    private float horiInput;

    private bool SwingDashed = false;
    public bool HasSwingDashed => SwingDashed;

    // Tracks the held->released edge so ReelIn can fire the one-shot release kick exactly once.
    private bool wasReeling;

    // Grappling something right next to the player collapses maxDistance/minDistance toward 0,
    // which turns the SpringJoint into a near-zero hard limit - any tiny movement then triggers
    // a violent correction force (the "Invalid worldAABB" physics explosion).
    private const float MinJointDistance = 1f;


    public override void OnStateEnter(PlayerStateManager gamestateManager)
    {
        base.OnStateEnter(gamestateManager);

        grapple = Grappleable.Resolve(manager.RUD.GrappledObject);

        manager.PBM.playerCanMove = false;
        SwingDashed = false;
        GrapplePoint = manager.RUD.GrapplePoint;
        manager.GrappleLr.enabled = true;
        manager.GrappleLr.positionCount = 2;

        // Calculate initial visual offset
        float currentOffset = manager.grappleStats.GrappleEnemyOffset;
        if (grapple != GrappleType.Normal)
        {
            if (manager.RUD.GrappledObject.TryGetComponent<Collider>(out Collider col))
            {
                currentOffset = Mathf.Max(col.bounds.extents.x, col.bounds.extents.z);
            }
        }

        Vector3 dirToPlayer = (manager.transform.position - GrapplePoint).normalized;
        Vector3 visualTarget = GrapplePoint + (dirToPlayer * currentOffset);

        manager.GrappleLr.SetPosition(1, visualTarget);
        InnitiateSpring();
    }

    public override void OnStateExit()
    {
        base.OnStateExit();
        manager.GrappleLr.enabled = false;
        MonoBehaviour.Destroy(joint);
    }

    public override void OnStateUpdate()
    {
        base.OnStateUpdate();

        manager.GuntipPointToGrapple();

        // 1. Determine true physical target
        Vector3 trueTarget = GrapplePoint;
        if (grapple == GrappleType.Light || grapple == GrappleType.Heavy)
        {
            trueTarget = manager.RUD.GrappledObject.transform.position;
            joint.connectedAnchor = trueTarget;
        }

        // 2. Calculate dynamic offset (checks enemy bounds)
        float currentOffset = manager.grappleStats.GrappleEnemyOffset;
        if (grapple != GrappleType.Normal)
        {
            if (manager.RUD.GrappledObject.TryGetComponent<Collider>(out Collider col))
            {
                currentOffset = Mathf.Max(col.bounds.extents.x, col.bounds.extents.z);
            }
        }

        // 3. Apply offset visual position to line renderer and hand
        Vector3 dirToPlayer = (manager.transform.position - trueTarget).normalized;
        Vector3 visualTarget = trueTarget + (dirToPlayer * currentOffset);

        manager.GrappleLr.SetPosition(1, visualTarget);

        // Inputs & State Changes
        if (!manager.Input.GrappleHeld)
        {
            switch (grapple)
            {
                case GrappleType.Normal:
                    manager.ChangeState(manager.pullRopeBackState);
                    break;
                case GrappleType.Light:
                    manager.ChangeState(manager.GrapplePullState);
                    break;
                case GrappleType.Heavy:
                    manager.ChangeState(manager.GrapplePullinState);
                    break;
            }
        }

        vertInput = manager.Input.Move.y;
        horiInput = manager.Input.Move.x;
        vertInput = new Vector2(horiInput, vertInput).normalized.y;
        horiInput = new Vector2(horiInput, vertInput).normalized.x;

        drawRope();
        AirControl(manager.Input.SprintHeld ? manager.grappleStats.GrappleAirSteerStrength : 1f);
        SwingDash();
        ReelIn(trueTarget);

        if (Vector3.Distance(GrapplePoint, manager.transform.position) < currentDist)
        {
            RecalibateJointMaxDistance();
        }
        currentDist = Vector3.Distance(GrapplePoint, manager.transform.position);
    }

    public override void OnStatePhysicsUpdate()
    {
        base.OnStatePhysicsUpdate();

        manager.rb.AddForce(Vector3.up * 5, ForceMode.Acceleration); // lower gravity for better air control
    }

    private void InnitiateSpring()
    {
        joint = manager.gameObject.AddComponent<SpringJoint>();
        joint.autoConfigureConnectedAnchor = false;
        joint.connectedAnchor = GrapplePoint;

        float distance = Mathf.Max(Vector3.Distance(manager.transform.position, GrapplePoint), MinJointDistance);
        joint.maxDistance = distance * 1f;
        joint.minDistance = distance * 0.25f;

        joint.spring = manager.grappleStats.JointSpring; // Adjust these for "bounciness"
        joint.damper = manager.grappleStats.JointDamper; // Adjust these to stop swinging forever
        joint.massScale = manager.grappleStats.JointMassScale;
    }

    private void drawRope()
    {
        if (!joint) return;

        manager.GrappleLr.SetPosition(0, manager.Guntip.position);
    }

    private void AirControl(float strengthMultiplier = 1f)
    {
        float vertical = vertInput * manager.grappleStats.AirControlFwdForce * strengthMultiplier;
        float horizontal = horiInput * manager.grappleStats.AirControlHorizontalForce * strengthMultiplier;

        Vector3 TotalForceDir = (manager.Cam.transform.forward * vertical) + (manager.Cam.transform.right * horizontal);
        manager.rb.AddForce(TotalForceDir * Time.deltaTime, ForceMode.Force);
    }

    private void SwingDash()
    {
        if (manager.Input.JumpPressed && !SwingDashed && manager.Energy.UseEnergy(manager.Energy.stats.GrappleDashUsage))
        {
            float dashForce = manager.rb.linearVelocity.magnitude * manager.grappleStats.SwingDashPower * currentDist * 0.001f;
            float initialVelocity = manager.rb.linearVelocity.magnitude;

            float newForce = dashForce + initialVelocity;

            newForce = Mathf.Clamp(newForce, manager.grappleStats.SwingDashMinPower, manager.grappleStats.SwingDashMaxPower);
            manager.rb.AddForce(manager.Cam.transform.forward.normalized * newForce, ForceMode.VelocityChange);
            SwingDashed = true;
        }
    }

    private void RecalibateJointMaxDistance()
    {
        joint.maxDistance = Mathf.Max(currentDist * 0.8f, MinJointDistance);
        joint.minDistance = Mathf.Max(currentDist * 0.25f, MinJointDistance * 0.25f);
    }

    // Echo Point Nova-style hybrid pull: accelerate toward the anchor up to a capped speed
    // (pullAccel/maxPullSpeed) rather than an open-ended force, partially bleed off sideways
    // momentum (lateralDamping) instead of either preserving it fully or snapping straight to
    // the rope, let air input steer mid-pull (airSteerStrength, applied to AirControl in
    // OnStateUpdate), and keep whatever's left when you let go - plus a small kick - instead of
    // just stopping dead (releaseBoost).
    private void ReelIn(Vector3 targetPoint)
    {
        bool reeling = manager.Input.SprintHeld && manager.Energy.UseEnergy(manager.Energy.stats.GrappleReelInUsage * Time.deltaTime);

        if (!reeling)
        {
            if (wasReeling)
            {
                Vector3 releaseDir = (targetPoint - manager.transform.position).normalized;
                manager.rb.AddForce(releaseDir * manager.grappleStats.GrappleReleaseBoost, ForceMode.VelocityChange);
            }
            wasReeling = false;
            return;
        }

        Vector3 toTarget = targetPoint - manager.transform.position;
        float distance = toTarget.magnitude;
        wasReeling = true;
        if (distance <= MinJointDistance) return;

        Vector3 pullDir = toTarget / distance;
        Vector3 velocity = manager.rb.linearVelocity;
        float pullSpeed = Vector3.Dot(velocity, pullDir);

        if (pullSpeed < manager.grappleStats.GrappleMaxPullSpeed)
        {
            float accel = manager.grappleStats.GrapplePullAccel * Time.deltaTime;
            manager.rb.AddForce(pullDir * accel, ForceMode.VelocityChange);
        }

        // Bleed sideways momentum toward a retained floor rather than toward zero - so some of
        // the original swing carries through - using a framerate-independent exponential decay
        // (rate, not a flat per-frame fraction) so it fades out smoothly instead of snapping.
        Vector3 lateralVelocity = velocity - pullDir * pullSpeed;
        Vector3 retainedLateral = lateralVelocity * manager.grappleStats.GrappleMomentumRetention;
        float decayLerp = 1f - Mathf.Exp(-manager.grappleStats.GrappleLateralDamping * Time.deltaTime);
        Vector3 newLateralVelocity = Vector3.Lerp(lateralVelocity, retainedLateral, decayLerp);
        manager.rb.AddForce(newLateralVelocity - lateralVelocity, ForceMode.VelocityChange);

        currentDist = Mathf.Max(distance - 0.1f, MinJointDistance);
        RecalibateJointMaxDistance();
    }
}