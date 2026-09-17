using System;

/// <summary>
/// Global event bus that decouples the player from the enemy system.
///
/// The original project refilled the player's energy by subscribing to
/// <c>BaseEnemy.OnAnyEnemyDied</c>. To keep this package free of any enemy class,
/// <see cref="PlayerEnergy"/> now listens to <see cref="OnTargetKilled"/> instead.
///
/// Wire it up in the host project by calling <see cref="RaiseTargetKilled"/> from
/// your own enemy/target death code, e.g.:
/// <code>
/// public void Death()
/// {
///     // ...your death logic...
///     PlayerCombatEvents.RaiseTargetKilled();
/// }
/// </code>
/// </summary>
public static class PlayerCombatEvents
{
    /// <summary>Fired whenever a grapple/combat target dies. PlayerEnergy refills on this.</summary>
    public static event Action OnTargetKilled;

    /// <summary>Raise <see cref="OnTargetKilled"/>. Call from your target/enemy death code.</summary>
    public static void RaiseTargetKilled() => OnTargetKilled?.Invoke();
}
