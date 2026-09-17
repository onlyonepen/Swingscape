/// <summary>
/// Contract for a swappable weapon / attack mode.
///
/// PlayerAttacking runs its built-in melee combo by default (currentMode == null).
/// Equipping an IWeaponMode via PlayerAttacking.EquipMode overrides how the primary
/// attack behaves, so new weapon behaviours can be added without changing the driver.
///
/// Nothing implements this yet — it's purely the extension point for future modes.
/// </summary>
public interface IWeaponMode
{
    /// <summary>Called when this mode becomes active.</summary>
    void OnEquip(PlayerAttacking owner);

    /// <summary>Called when this mode is swapped out.</summary>
    void OnUnequip();

    /// <summary>Called when the primary-attack input is pressed this frame.</summary>
    void OnPrimaryPressed();

    /// <summary>Per-frame update while equipped (windup timers, charge, etc.).</summary>
    void Tick();
}
