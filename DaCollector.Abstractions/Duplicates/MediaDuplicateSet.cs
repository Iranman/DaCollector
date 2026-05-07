using System.Collections.Generic;

namespace DaCollector.Abstractions.Duplicates;

/// <summary>
/// Possible duplicate Plex media entries grouped by shared metadata signals.
/// </summary>
public sealed record MediaDuplicateSet
{
    /// <summary>
    /// Stable duplicate set key.
    /// </summary>
    public string DuplicateKey { get; init; } = string.Empty;

    /// <summary>
    /// Highest-confidence signal for this set.
    /// </summary>
    public MediaDuplicateMatchType PrimaryMatchType { get; init; } = MediaDuplicateMatchType.Unknown;

    /// <summary>
    /// Confidence score from 0 to 100.
    /// </summary>
    public int Score { get; init; }

    /// <summary>
    /// Number of Plex entries in the set.
    /// </summary>
    public int CandidateCount { get; init; }

    /// <summary>
    /// Whether DaCollector can safely delete one entry automatically.
    /// </summary>
    public bool SafeDeleteCandidate { get; init; }

    /// <summary>
    /// Recommended action for the operator.
    /// </summary>
    public string ReviewAction { get; init; } = "Review in Plex before deleting or merging media entries.";

    /// <summary>
    /// Human-readable signals that caused this set to be flagged.
    /// </summary>
    public IReadOnlyList<string> ScoringReasons { get; init; } = [];

    /// <summary>
    /// Plex entries that may represent the same movie or show.
    /// </summary>
    public IReadOnlyList<MediaDuplicateItem> Items { get; init; } = [];
}
