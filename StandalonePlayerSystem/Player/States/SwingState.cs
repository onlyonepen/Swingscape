using Script.Enemy;
using UnityEngine;

public class SwingState : PlayerState
{
    public override float EnergyRegenRate => 0f;

    private enum GrappleItem
    {
        Terrain,
        Light,
        Heavy
    }

    private GrappleItem grapple;
    
    private Vector3 GrapplePoint;
    private SpringJoint joint;
    private float currentDist;

    private float vertInput;
    private float horiInput;

    private bool SwingDashed = false;
    

    public override void OnStateEnter(PlayerStateManager gamestateManager)
    {
        base.OnStateEnter(gamestateManager);

        int hitLayerBit = 1 << manager.RUD.GrappledObject.layer;
        if ((hitLayerBit & GlobalReference.Instance.EnemyLayer) != 0)
        {
            if ((hitLayerBit & manager.Targeting.HeavyPull) != 0) grapple = GrappleItem.Heavy;
            else grapple = GrappleItem.Light;
        }
        else grapple = GrappleItem.Terrain;
        
        manager.PBM.playerCanMove = false;
        SwingDashed = false;
        GrapplePoint = manager.RUD.GrapplePoint;
        manager.GrappleLr.enabled = true;
        manager.GrappleLr.positionCount = 2;

        // Calculate initial visual offset
        float currentOffset = manager.GrappleEnemyOffset;
        if (manager.RUD.GrappledObject != null && ((1 << manager.RUD.GrappledObject.layer) & GlobalReference.Instance.EnemyLayer) != 0)
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
        if (grapple == GrappleItem.Light || grapple == GrappleItem.Heavy)
        {
            trueTarget = manager.RUD.GrappledObject.transform.position;
            joint.connectedAnchor = trueTarget;
        }

        // 2. Calculate dynamic offset (checks enemy bounds)
        float currentOffset = manager.GrappleEnemyOffset;
        if (manager.RUD.GrappledObject != null && ((1 << manager.RUD.GrappledObject.layer) & GlobalReference.Instance.EnemyLayer) != 0)
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
                case GrappleItem.Terrain:
                    manager.ChangeState(manager.pullRopeBackState);
                    break;
                case GrappleItem.Light:
                    manager.ChangeState(manager.GrapplePullState);
                    break;
                case GrappleItem.Heavy:
                    manager.ChangeState(manager.GrapplePullinState);
                    break;
            }
        }
        
        if (manager.Input.SprintPressed && manager.Energy.UseEnergy(manager.Energy.GrappleLeapUsage))
        {
            manager.ChangeState(manager.GrappleLeapState);
            if (grapple == GrappleItem.Light)
            {
                Vector3 toplayer = manager.transform.position - GrapplePoint;
                float playerAffectedWeight = 35;
                if (manager.RUD.GrappledObject.TryGetComponent<IGrappleInteractable>(out var interactable)) interactable.Stagger(2f);
                manager.RUD.GrappledObject.GetComponent<Rigidbody>().AddForce(toplayer.normalized * playerAffectedWeight, ForceMode.Impulse);
            }
        }

        vertInput = manager.Input.Move.y;
        horiInput = manager.Input.Move.x;
        vertInput = new Vector2(horiInput, vertInput).normalized.y;
        horiInput = new Vector2(horiInput, vertInput).normalized.x;

        drawRope();
        AirControl();
        SwingDash();

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

        float distance = Vector3.Distance(manager.transform.position, GrapplePoint);
        joint.maxDistance = distance * 1f;
        joint.minDistance = distance * 0.25f;

        joint.spring = manager.JointSpring; // Adjust these for "bounciness"
        joint.damper = manager.JointDamper; // Adjust these to stop swinging forever
        joint.massScale = manager.JointMassScale;
    }

    private void drawRope()
    {
        if (!joint) return;

        manager.GrappleLr.SetPosition(0, manager.Guntip.position);
    }

    private void AirControl()
    {
        float vertical = vertInput * manager.AirControlFwdForce;
        float horizontal = horiInput * manager.AirControlHorizontalForce;

        Vector3 TotalForceDir = (manager.Cam.transform.forward * vertical) + (manager.Cam.transform.right * horizontal);
        manager.rb.AddForce(TotalForceDir * Time.deltaTime, ForceMode.Force);
    }

    private void SwingDash()
    {
        if (manager.Input.JumpPressed && !SwingDashed && manager.Energy.UseEnergy(manager.Energy.GrappleDashUsage))
        {
            float dashForce = manager.rb.linearVelocity.magnitude * manager.SwingDashPower * currentDist * 0.001f;
            float initialVelocity = manager.rb.linearVelocity.magnitude;

            float newForce = dashForce + initialVelocity;

            newForce = Mathf.Clamp(newForce, manager.SwingDashMinPower, manager.SwingDashMaxPower);
            manager.rb.AddForce(manager.Cam.transform.forward.normalized * newForce, ForceMode.VelocityChange);
            SwingDashed = true;
        }
    }

    private void RecalibateJointMaxDistance()
    {
        joint.maxDistance = currentDist * 0.8f;
        joint.minDistance = currentDist * 0.25f;
    }
}