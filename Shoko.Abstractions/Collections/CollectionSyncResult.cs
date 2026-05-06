using System.Collections.Generic;

namespace Shoko.Abstractions.Collections;

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
    /// Non-fatal issues encountered while evaluating the collection.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
