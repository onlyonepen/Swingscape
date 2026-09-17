using UnityEngine;

public class PullBackRopeState : PlayerState
{
    private float pullTime = 0.1f;
    public override void OnStateEnter(PlayerStateManager gamestateManager)
    {
        base.OnStateEnter(gamestateManager);
        manager.GrappleLr.enabled = true;
        manager.GrappleLr.positionCount = 2;
    }
    public override void OnStateUpdate()
    {
        base.OnStateUpdate();
        
        float elapsed = Time.time - stateEnterTime;
        float percent = Mathf.Clamp01(elapsed / pullTime);
        DrawRope(percent);

        manager.GrappleLr.SetPosition(0, manager.Guntip.position);

        if(Time.time - stateEnterTime > pullTime)
        {
            manager.ChangeState(manager.BaseState);
        }
    }

    public override void OnStateExit()
    {
        base.OnStateExit();

        manager.GrappleLr.enabled = false;
    }

    void DrawRope(float percent)
    {
        Vector3 origin = manager.RUD.GrapplePoint;
        //if (origin == Vector3.zero) origin = manager.Guntip.position + manager.Cam.transform.forward * manager.GrappleMaxDistance;
        Vector3 targetGoal = manager.Guntip.position;
        Vector3 currentTipPos = Vector3.Lerp(origin, targetGoal, percent);
        manager.GrappleLr.SetPosition(1, currentTipPos);
    }
}
