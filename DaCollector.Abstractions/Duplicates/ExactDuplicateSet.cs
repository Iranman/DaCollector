using System.Collections.Generic;

namespace DaCollector.Abstractions.Duplicates;

/// <summary>
/// Files that share the same hash and size.
/// </summary>
public sealed record ExactDuplicateSet
{
    /// <summary>
    /// Stable duplicate set key.
    /// </summary>
    public string DuplicateKey { get; init; } = string.Empty;

    /// <summary>
    /// Hash algorithm used for exact duplicate detection.
    /// </summary>
    public string HashType { get; init; } = "ED2K";

    /// <summary>
    /// File hash shared by every member.
    /// </summary>
    public string Hash { get; init; } = string.Empty;

    /// <summary>
    /// File size shared by every member.
    /// </summary>
    public long FileSize { get; init; }

    /// <summary>
    /// Number of stored locations in the set.
    /// </summary>
    public int LocationCount { get; init; }

    /// <summary>
    /// Number of stored locations that exist on disk.
    /// </summary>
    public int AvailableLocationCount { get; init; }

    /// <summary>
    /// Internal video record IDs participating in the set.
    /// </summary>
    public IReadOnlyList<int> VideoIDs { get; init; } = [];

    /// <summary>
    /// Suggested file location to keep.
    /// </summary>
    public int? SuggestedKeepLocationID { get; init; }

    /// <summary>
    /// Suggested duplicate file locations to remove.
    /// </summary>
    public IReadOnlyList<int> SuggestedRemoveLocationIDs { get; init; } = [];

    /// <summary>
    /// Stored file locations in this duplicate set.
    /// </summary>
    public IReadOnlyList<ExactDuplicateLocation> Locations { get; init; } = [];
}
