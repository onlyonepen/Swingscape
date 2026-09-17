using UnityEngine;

/// <summary>
/// The player's input contract. Everything that needs input (movement, states,
/// combat) depends on THIS, never on UnityEngine.Input directly.
///
/// To move to Unity's new Input System later: write a second component that
/// implements this interface (e.g. reading InputActions), put it on the player
/// instead of <see cref="PlayerInput"/>, and nothing else has to change —
/// PlayerManager resolves whichever component implements IPlayerInput.
/// </summary>
public interface IPlayerInput
{
    // --- Axes ---
    Vector2 Move { get; }     // WASD, smoothed (GetAxis)
    Vector2 MoveRaw { get; }  // WASD, unsmoothed (GetAxisRaw)
    Vector2 Look { get; }     // mouse delta, before sensitivity

    // --- Jump ---
    bool JumpPressed { get; }
    bool JumpHeld { get; }

    // --- Crouch / slide (LeftControl also doubles as "downward" while wall-running) ---
    bool CrouchPressed { get; }
    bool CrouchReleased { get; }
    bool CrouchHeld { get; }

    // --- Combat ---
    bool AttackPressed { get; }

    // --- Grapple (right mouse) ---
    bool GrapplePressed { get; }
    bool GrappleHeld { get; }

    // --- Sprint / grapple-leap (LeftShift; also "upward" while wall-running) ---
    bool SprintPressed { get; }
    bool SprintHeld { get; }

    // --- Discrete movement keys still read literally by some states ---
    bool ForwardHeld { get; }
    bool ForwardReleased { get; }
    bool StrafeLeftHeld { get; }
    bool StrafeRightHeld { get; }

    // --- Meta ---
    bool RespawnPressed { get; }
}
