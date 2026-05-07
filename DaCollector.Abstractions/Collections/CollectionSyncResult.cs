using System.Collections.Generic;
using DaCollector.Abstractions.MediaServers.Plex;

namespace DaCollector.Abstractions.Collections;

/// <summary>
/// Result of evaluating one managed collection during a sync run.
/// </summary>
public sealed record CollectionSyncResult
{
    /// <summary>
    /// Collection definition evaluated for this result.
    /// </summary>
    public CollectionDefinition Collection { get; init; } = new();

    /// <summary>
    /// Sync mode requested by the collection definition.
    /// </summary>
    public CollectionSyncMode RequestedSyncMode { get; init; } = CollectionSyncMode.Preview;

    /// <summary>
    /// Sync mode actually used by this run.
    /// </summary>
    public CollectionSyncMode EffectiveSyncMode { get; init; } = CollectionSyncMode.Preview;

    /// <summary>
    /// Whether membership changes were applied to a target library.
    /// </summary>
    public bool Applied { get; init; }

    /// <summary>
    /// Target library adapter used by this run.
    /// </summary>
    public string Target { get; init; } = "preview";

    /// <summary>
    /// Distinct media items resolved for this collection.
    /// </summary>
    public IReadOnlyList<CollectionBuilderPreviewItem> Items { get; init; } = [];

    /// <summary>
    /// Number of resolved items matched in the target library.
    /// </summary>
    public int MatchedItemCount { get; init; }

    /// <summary>
    /// Number of resolved items missing from the target library.
    /// </summary>
    public int MissingItemCount { get; init; }

    /// <summary>
    /// Number of target items added during the apply run.
    /// </summary>
    public int AddedItemCount { get; init; }

    /// <summary>
    /// Number of target items removed during the apply run.
    /// </summary>
    public int RemovedItemCount { get; init; }

    /// <summary>
    /// Plex diff from the apply or dry-run, when a Plex target was evaluated.
    /// </summary>
    public PlexCollectionApplyResult? PlexDiff { get; init; }

    /// <summary>
    /// Non-fatal issues encountered while evaluating the collection.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
