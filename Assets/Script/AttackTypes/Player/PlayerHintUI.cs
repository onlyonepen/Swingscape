using System.Text;
using TMPro;
using UnityEngine;

/// <summary>Reads the player's current locomotion/attack state and shows the relevant
/// control prompt(s) in a TextMeshPro field, e.g. "Right click to grapple".</summary>
public class PlayerHintUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI hintText;

    private PlayerStateManager stateManager;
    private PlayerAttacking attacking;

    private readonly StringBuilder builder = new StringBuilder();

    private void Start()
    {
        stateManager = GlobalReference.Instance.player.Locomotion;
        attacking = GlobalReference.Instance.player.Combat;
    }

    private void Update()
    {
        builder.Clear();

        PlayerState current = stateManager.CurrentState;

        if (current == stateManager.BaseState && stateManager.canGrapple)
        {
            builder.AppendLine("Right click to grapple");
        }

        if (attacking.CurrentAttackState == PlayerAttackState.Idle)
        {
            builder.AppendLine("Left click to attack");
        }

        if (current == stateManager.SwingState)
        {
            if (!((SwingState)current).HasSwingDashed)
            {
                builder.AppendLine("Spacebar to grapple boost");
            }

            builder.AppendLine("Shift to grapple jump");

            GrappleType grappleType = Grappleable.Resolve(stateManager.RUD.GrappledObject);
            if (grappleType == GrappleType.Light)
            {
                builder.AppendLine("Release to pull reel object in");
            }
            else if (grappleType == GrappleType.Heavy)
            {
                builder.AppendLine("Release to pull yourself toward object");
            }
        }

        hintText.text = builder.ToString().TrimEnd();
    }
}
