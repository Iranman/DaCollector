namespace DaCollector.Abstractions.Duplicates;

/// <summary>
/// Summary counters for exact duplicate detection.
/// </summary>
public sealed record ExactDuplicateSummary
{
    /// <summary>
    /// Number of exact duplicate sets found.
    /// </summary>
    public int SetCount { get; init; }

    /// <summary>
    /// Number of file locations in duplicate sets.
    /// </summary>
    public int LocationCount { get; init; }

    /// <summary>
    /// Number of remove candidates after keeping one location per set.
    /// </summary>
    public int SuggestedRemoveLocationCount { get; init; }

    /// <summary>
    /// Total reclaimable bytes if every suggested remove candidate is deleted.
    /// </summary>
    public long PotentialReclaimBytes { get; init; }

    /// <summary>
    /// Number of duplicate locations that currently exist on disk.
    /// </summary>
    public int AvailableLocationCount { get; init; }
}
