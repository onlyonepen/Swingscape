using System.Collections;
using UnityEngine;

namespace Script.Player.States
{
    public class DiedState : PlayerState
    {
        public override void OnStateEnter(PlayerStateManager gamestateManager)
        {
            base.OnStateEnter(gamestateManager);
            manager.PBM.enabled = false;
            manager.StartCoroutine(CinematicDeathRoutine());
        }

        public override void OnStateExit()
        {
            base.OnStateExit();
            manager.PBM.enabled = true;
        }

        public override void OnStateUpdate()
        {
            base.OnStateUpdate();
            if (manager.Input.RespawnPressed)
            {
                Time.timeScale = 1f;
                GameUIManager.Instance.ShowGameOverScreen(false);
                GameUIManager.Instance.ShowRedScreenOverlay(false);

                Transform checkpoint = CheckpointManager.Instance.CurrentCheckpoint;
                manager.TeleportTo(checkpoint.position, checkpoint.rotation);

                manager.playerHp.Revive();
                manager.ChangeState(manager.BaseState);
            }
        }

        private IEnumerator CinematicDeathRoutine()
        {
            float elapsedTime = 0f;

            // Start completely clear, end completely red (adjust the alpha to taste)
            const float startAlpha = 0f;
            const float endAlpha = 0.65f;

            // Ensure the overlay is active before we start tweaking its alpha
            GameUIManager.Instance.ShowRedScreenOverlay(true);

            while (elapsedTime < manager.deathDuration)
            {
                // Crucial: Use unscaledDeltaTime so the lerp doesn't slow itself down!
                elapsedTime += Time.unscaledDeltaTime;
                float t = elapsedTime / manager.deathDuration;

                // 1. Lerp time down to a dead stop
                Time.timeScale = Mathf.Lerp(1f, 0f, t);

                // 2. Lerp the screen to red
                GameUIManager.Instance.SetRedScreenAlpha(Mathf.Lerp(startAlpha, endAlpha, t));

                yield return null;
            }

            // Lock final values to prevent floating point inaccuracies
            Time.timeScale = 0f;
            GameUIManager.Instance.SetRedScreenAlpha(endAlpha);

            // 3. Activate the final Game Over UI menus
            GameUIManager.Instance.ShowGameOverScreen(true);
        }
    }
}