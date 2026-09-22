using UnityEngine;

public class GrappleLeapState : PlayerState
{
    float enterTimeStamp;
    bool isExtraGravOn = false;
    
    public override void OnStateEnter(PlayerStateManager gamestateManager)
    {
        base.OnStateEnter(gamestateManager);

        manager.playerHp.TurnOnInvulnerability();
        
        enterTimeStamp = Time.time;

        manager.PBM.playerCanMove = false;
        manager.PBM.FloatingCapsuleActive = false;
        if (manager.PBM.hasFallingExtraGrav)
        {
            isExtraGravOn = true;
            manager.PBM.hasFallingExtraGrav = false;
        }

        manager.GrappleLr.enabled = true;
        manager.GrappleLr.positionCount = segmentCount;

        manager.rb.linearVelocity = Vector3.zero;

        JumpToGrapplePos(); 
    }

    public override void OnStateUpdate()
    {
        base.OnStateUpdate();

        manager.GuntipPointToGrapple();

        Vector3 trueTarget = manager.RUD.GrapplePoint;
        float currentOffset = manager.grappleStats.GrappleEnemyOffset;

        // Check if object is an enemy and dynamically calculate offset from collider size
        if (Grappleable.Resolve(manager.RUD.GrappledObject) != GrappleType.Normal)
        {
            trueTarget = manager.RUD.GrappledObject.transform.position;

            if (manager.RUD.GrappledObject.TryGetComponent<Collider>(out Collider col))
            {
                currentOffset = Mathf.Max(col.bounds.extents.x, col.bounds.extents.z);
            }
        }
        
        Vector3 dirToPlayer = (manager.transform.position - trueTarget).normalized;
        Vector3 visualTarget = trueTarget + (dirToPlayer * currentOffset);


        AirControl();

        float elapsed = Time.time - stateEnterTime;
        float percent = Mathf.Clamp01(elapsed / 0.15f);
        
        DrawTuggingRope(percent, visualTarget);

        if(Time.time - enterTimeStamp > 0.5f)
        {
            manager.ChangeState(manager.pullRopeBackState);
        }

        manager.GrappleLr.SetPosition(0, manager.Guntip.position);
    }

    public override void OnStateExit()
    {
        base.OnStateExit();

        manager.playerHp.TurnOffInvulnerability();

        manager.GrappleLr.enabled = false;
        manager.GrappleLr.positionCount = 2;

        if (isExtraGravOn) manager.PBM.hasFallingExtraGrav = true;
        manager.PBM.FloatingCapsuleActive = true;
    }

    public override void OnStateTriggerEnter(Collider collider)
    {
        base.OnStateTriggerEnter(collider);
        manager.ChangeState(manager.pullRopeBackState);
    }

    private void AirControl()
    {
        float vertical = manager.Input.Move.y * manager.grappleStats.AirControlFwdForce;
        float horizontal = manager.Input.Move.x * manager.grappleStats.AirControlHorizontalForce;

        Vector3 TotalForceDir = (manager.Cam.transform.forward * vertical) + (manager.Cam.transform.right * horizontal);
        manager.rb.AddForce(TotalForceDir * Time.deltaTime, ForceMode.Force);
    }

    private void JumpToGrapplePos()
    {
        Vector3 lowestPoint = new Vector3(manager.transform.position.x, manager.transform.position.y,
                                manager.transform.position.z);

        float grapplePointRelativeYPos = manager.RUD.GrapplePoint.y - lowestPoint.y;
        float highestPointOnArc = grapplePointRelativeYPos + manager.grappleStats.OvershootYAxis;

        if (grapplePointRelativeYPos < 0) highestPointOnArc = manager.grappleStats.OvershootYAxis;

        manager.rb.linearVelocity = calculateJumpVelocity(manager.transform.position, manager.RUD.GrapplePoint, highestPointOnArc);
    }

    private Vector3 calculateJumpVelocity(Vector3 startPoint, Vector3 endPoint, float trajectoryHeight) =>
        ProjectileMath.CalculateArcVelocity(startPoint, endPoint, trajectoryHeight);

    public int segmentCount = 30;
    public float maxTugAmplitude = .75f; 

    void DrawTuggingRope(float percent, Vector3 visualTarget)
    {
        if(percent > 1)
        {
            manager.GrappleLr.positionCount = 2;
            manager.GrappleLr.SetPosition(0, manager.Guntip.position);
            manager.GrappleLr.SetPosition(1, visualTarget);
            return;
        }

        Vector3 origin = manager.Guntip.position;
        manager.GrappleLr.positionCount = segmentCount;

        float animationLift = Mathf.Sin(percent * Mathf.PI * 2) * maxTugAmplitude;

        for (int i = 0; i < segmentCount; i++)
        {
            float t = i / (float)(segmentCount - 1);
            Vector3 pos = Vector3.Lerp(origin, visualTarget, t);
            float curveShape = Mathf.Sin(t * Mathf.PI);
            pos += manager.transform.up * (curveShape * animationLift);

            manager.GrappleLr.SetPosition(i, pos);
        }
    }
}