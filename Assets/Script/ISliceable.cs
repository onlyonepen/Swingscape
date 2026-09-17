using JL.Splitting;

/// <summary>Optional reaction hook for objects that need custom behavior around being sliced.
/// SliceAttackTypeSO owns the actual mesh splitting and works without this interface (e.g. on a
/// plain cube); implement it only when an object needs to react (death sequence, extra force, ...).</summary>
public interface ISliceable
{
    /// <summary>Called synchronously right before the mesh split starts.</summary>
    void OnSliceStart();

    /// <summary>Called once the mesh split has finished and the two halves exist in the scene.</summary>
    void OnSliceComplete(SplitResult result);
}
