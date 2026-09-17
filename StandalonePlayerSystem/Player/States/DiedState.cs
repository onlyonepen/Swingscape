using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

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

        public override void OnStateUpdate()
        {
            base.OnStateUpdate();
            if (manager.Input.RespawnPressed)
            {
                Time.timeScale = 1f;
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            }
        }

        private IEnumerator CinematicDeathRoutine()
        {
            float elapsedTime = 0f;
        
            // Start completely clear, end completely red (adjust the alpha to taste)
            Color startColor = new Color(1f, 0f, 0f, 0f); 
            Color endColor = new Color(1f, 0f, 0f, 0.65f); 

            // Ensure the overlay is active before we start tweaking its alpha
            manager.redScreenOverlay.gameObject.SetActive(true);

            while (elapsedTime < manager.deathDuration)
            {
                // Crucial: Use unscaledDeltaTime so the lerp doesn't slow itself down!
                elapsedTime += Time.unscaledDeltaTime; 
                float t = elapsedTime / manager.deathDuration;

                // 1. Lerp time down to a dead stop
                Time.timeScale = Mathf.Lerp(1f, 0f, t);

                // 2. Lerp the screen to red
                manager.redScreenOverlay.color = Color.Lerp(startColor, endColor, t);

                yield return null;
            }

            // Lock final values to prevent floating point inaccuracies
            Time.timeScale = 0f;
            manager.redScreenOverlay.color = endColor;

            // 3. Activate the final Game Over UI menus
            manager.gameOverScreen.SetActive(true);
        }
    }
}