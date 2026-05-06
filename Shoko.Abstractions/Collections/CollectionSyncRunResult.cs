using System;
using System.Collections.Generic;

namespace Shoko.Abstractions.Collections;

/// <summary>
/// Result of a managed collection sync run.
/// </summary>
public sealed record CollectionSyncRunResult
{
    /// <summary>
    /// Unique ID for this sync run.
    /// </summary>
    public Guid RunID { get; init; }

    /// <summary>
    /// UTC timestamp when the run started.
    /// </summary>
    public DateTimeOffset StartedAt { get; init; }

    /// <summary>
    /// UTC timestamp when the run finished.
    /// </summary>
    public DateTimeOffset FinishedAt { get; init; }

    /// <summary>
    /// Number of enabled collection definitions seen by the run.
    /// </summary>
    public int EnabledCollectionCount { get; init; }

    /// <summary>
    /// Number of disabled collection definitions skipped by the run.
    /// </summary>
    public int DisabledCollectionCount { get; init; }

    /// <summary>
    /// Total distinct preview items across evaluated collections.
    /// </summary>
    public int TotalItemCount { get; init; }

    /// <summary>
    /// Results for each evaluated collection.
    /// </summary>
    public IReadOnlyList<CollectionSyncResult> Collections { get; init; } = [];

    /// <summary>
    /// Run-level warnings.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
