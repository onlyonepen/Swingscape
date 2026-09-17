/// <summary>
/// Decoupling seam between the player and whatever it can grapple/pull (enemies,
/// physics props, etc.). In the original Swingscape project the player referenced
/// the concrete <c>BaseEnemy</c> class directly; that hard dependency has been
/// replaced by this interface so the player package carries NO knowledge of any
/// enemy system.
///
/// Implement this on any object the grapple should be able to interact with, then
/// put that object on the layer assigned to <see cref="GlobalReference.EnemyLayer"/>
/// (or your "pullable" layers configured in <c>GrappleTargeting</c>).
/// </summary>
public interface IGrappleInteractable
{
    /// <summary>Called the instant the player begins pulling this object toward them
    /// (GrapplePullState / GrapplePullintoState). Use it to enter a "staggered/pulled"
    /// reaction so the object stops fighting the pull.</summary>
    void GetPull();

    /// <summary>Called when the player swing-leaps off this object (SwingState). Should
    /// briefly stun / ragdoll the target. <paramref name="time"/> is the stagger duration.</summary>
    void Stagger(float time = 120f);
}
