using UnityEngine;
public class ThrowGrappleState : PlayerState
{
    RaycastHit grappleCastHit;

    private int segmentCount = 40;
    private float waveSize = 1f;
    private float waveFrequency = 2f;
    private float waveSpeed = 15f;

    private Vector3 InitialHitPos;
    
    public override void OnStateEnter(PlayerStateManager gamestateManager)
    {
        base.OnStateEnter(gamestateManager);
        
        if (!manager.canGrapple)
        {
            manager.ChangeState(manager.BaseState);
            return;
        }
        
        AudioManager.Instance.PlayAudioByName("ThrowGrapple", manager.transform.position, true);
        
        manager.PBM.enabled = true;

        grappleCastHit = manager.Targeting.Predict();
        manager.Targeting.HidePredictionPoint();

        manager.GrappleLr.enabled = true;
        manager.GrappleLr.positionCount = segmentCount;

        if(grappleCastHit.collider != null)
        {
            manager.RUD.GrappledObject = grappleCastHit.collider.gameObject;
            manager.RUD.GrapplePoint = grappleCastHit.point;
        }
        else
        {
            manager.RUD.GrapplePoint = manager.Guntip.position + manager.Cam.transform.forward * manager.Targeting.GrappleMaxDistance;
        }
    }

    public override void OnStateUpdate()
    {
        base.OnStateUpdate();

        manager.GuntipPointToGrapple();

        float elapsed = Time.time - stateEnterTime;
        float percent = Mathf.Clamp01(elapsed / manager.GrappleTravelTime);

        DrawAnimatedRope(percent);
        
        if (Time.time - stateEnterTime > manager.GrappleTravelTime)
        { 
            if ( grappleCastHit.collider == null)
            {
                //manager.RUD.GrapplePoint = manager.Guntip.position + manager.Cam.transform.forward * manager.GrappleMaxDistance;
                manager.ChangeState(manager.pullRopeBackState);
                return;
            }

            LayerMask combinedLayer = manager.Targeting.Swingable | manager.Targeting.Pullable | manager.Targeting.HeavyPull;
            if ((1 << manager.RUD.GrappledObject.layer & combinedLayer) != 0) { manager.ChangeState(manager.SwingState); }
            else manager.ChangeState(manager.pullRopeBackState);
        }
    }

    public override void OnStateExit()
    {
        base.OnStateExit();

        manager.GrappleLr.enabled = false;
        manager.GrappleLr.positionCount = 2;
    }


    private void DrawAnimatedRope(float percent)
    {
        Vector3 origin = manager.Guntip.position;
        Vector3 targetGoal = manager.RUD.GrapplePoint;
        var grappledObj = manager.RUD.GrappledObject;
        
        if (grappledObj != null) 
        {
            // 3. Explicitly group the bitshift (1 << layer) for better readability
            if (((1 << grappledObj.layer) & GlobalReference.Instance.EnemyLayer) != 0) 
            {
                targetGoal = grappledObj.transform.position;
            }
        }
        else return; 
        
        Vector3 currentTipPos = Vector3.Lerp(origin, targetGoal, percent);

        for (int i = 0; i < segmentCount; i++)
        {
            float t = i / (float)(segmentCount - 1);
            Vector3 pos = Vector3.Lerp(origin, currentTipPos, t);

            if (percent < 0.99f)
            {
                float taper = Mathf.Sin(t * Mathf.PI);

                float wave = Mathf.Sin(t * Mathf.PI * waveFrequency + (Time.time * waveSpeed))
                             * waveSize
                             * (1 - percent)
                             * taper;

                pos += manager.transform.up * wave;
                pos += manager.transform.right * (wave * 0.5f);
            }

            manager.GrappleLr.SetPosition(i, pos);
        }
    }
}