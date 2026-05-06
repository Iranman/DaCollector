namespace Shoko.Abstractions.Collections;

/// <summary>
/// How a managed collection should apply evaluated rule results.
/// </summary>
public enum CollectionSyncMode : int
{
    /// <summary>
    /// Compute changes without saving collection membership.
    /// </summary>
    Preview = 0,

    /// <summary>
    /// Add matched items and keep existing members.
    /// </summary>
    Append = 1,

    /// <summary>
    /// Add matched items and remove managed members that no longer match.
    /// </summary>
    Sync = 2,
}
