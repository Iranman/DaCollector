using System.Collections.Generic;
using DaCollector.Abstractions.Collections;

namespace DaCollector.Abstractions.MediaServers.Plex;

/// <summary>
/// Result of applying a managed collection to a Plex library section.
/// </summary>
public sealed record PlexCollectionApplyResult
{
    /// <summary>
    /// Plex library section key used for applying membership.
    /// </summary>
    public string SectionKey { get; init; } = string.Empty;

    /// <summary>
    /// Plex collection title managed by this apply run.
    /// </summary>
    public string CollectionName { get; init; } = string.Empty;

    /// <summary>
    /// Sync mode used for this apply run.
    /// </summary>
    public CollectionSyncMode SyncMode { get; init; } = CollectionSyncMode.Preview;

    /// <summary>
    /// Whether the apply run wrote membership changes to Plex.
    /// </summary>
    public bool Applied { get; init; }

    /// <summary>
    /// Whether this was a dry run that computed the diff without writing changes.
    /// </summary>
    public bool DryRun { get; init; }

    /// <summary>
    /// Match result used by the apply run.
    /// </summary>
    public PlexCollectionMatch Match { get; init; } = new();

    /// <summary>
    /// Number of items already in the Plex collection before applying changes.
    /// </summary>
    public int ExistingItemCount { get; init; }

    /// <summary>
    /// Number of Plex items newly tagged into the collection.
    /// </summary>
    public int AddedItemCount { get; init; }

    /// <summary>
    /// Number of Plex items removed from the collection.
    /// </summary>
    public int RemovedItemCount { get; init; }

    /// <summary>
    /// Number of matched Plex items that were already in the collection.
    /// </summary>
    public int UnchangedItemCount { get; init; }

    /// <summary>
    /// Plex items added to the collection.
    /// </summary>
    public IReadOnlyList<PlexMediaItem> Added { get; init; } = [];

    /// <summary>
    /// Plex items removed from the collection.
    /// </summary>
    public IReadOnlyList<PlexMediaItem> Removed { get; init; } = [];

    /// <summary>
    /// Non-fatal issues encountered while applying the collection.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
