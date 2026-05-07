using System.Collections.Generic;

namespace DaCollector.Abstractions.Duplicates;

/// <summary>
/// Non-destructive cleanup recommendation for one exact duplicate set.
/// </summary>
public sealed record ExactDuplicateCleanupPlan
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
    /// Number of stored locations in the duplicate set.
    /// </summary>
    public int LocationCount { get; init; }

    /// <summary>
    /// Number of stored locations that currently exist on disk.
    /// </summary>
    public int AvailableLocationCount { get; init; }

    /// <summary>
    /// Recommended location to keep.
    /// </summary>
    public ExactDuplicateLocation? KeepLocation { get; init; }

    /// <summary>
    /// Recommended duplicate locations to remove.
    /// </summary>
    public IReadOnlyList<ExactDuplicateLocation> RemoveCandidates { get; init; } = [];

    /// <summary>
    /// Number of recommended remove candidates.
    /// </summary>
    public int RemoveCandidateCount { get; init; }

    /// <summary>
    /// Number of recommended remove candidates that currently exist on disk.
    /// </summary>
    public int AvailableRemoveCandidateCount { get; init; }

    /// <summary>
    /// Bytes that could be reclaimed by deleting available remove candidates.
    /// </summary>
    public long PotentialReclaimBytes { get; init; }

    /// <summary>
    /// Notes that should be shown before applying cleanup.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
