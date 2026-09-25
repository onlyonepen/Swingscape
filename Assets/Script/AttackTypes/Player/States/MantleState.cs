using DG.Tweening;
using UnityEngine;

public class MantleState : PlayerState
{
    public override void OnStateEnter(PlayerStateManager gamestateManager)
    {
        base.OnStateEnter(gamestateManager);
        manager.PBM.playerCanMove = false;
        manager.rb.linearVelocity = Vector3.zero;

        manager.Targeting.HidePredictionPoint();

        manager.transform.DOMove(manager.RUD.MantlePoint, 0.3f);
    }

    public override void OnStateExit()
    {
        base.OnStateExit();
    }

    public override void OnStateTriggerEnter(Collider collider)
    {
        base.OnStateTriggerEnter(collider);
    }

    public override void OnStateUpdate()
    {
        base.OnStateUpdate();
        manager.WaitToChangeState(manager.BaseState, 0.3f, stateEnterTime);
    }
}
