using System.Collections.Generic;
using System;

namespace Shoko.Abstractions.Collections;

/// <summary>
/// Portable description of a managed movie or TV collection.
/// </summary>
public sealed record CollectionDefinition
{
    /// <summary>
    /// Stable identifier for this managed collection definition.
    /// </summary>
    public Guid ID { get; init; }

    /// <summary>
    /// Collection display name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Whether this collection definition should run.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Membership sync behavior.
    /// </summary>
    public CollectionSyncMode SyncMode { get; init; } = CollectionSyncMode.Preview;

    /// <summary>
    /// Rules used to evaluate collection membership.
    /// </summary>
    public IReadOnlyList<CollectionRule> Rules { get; init; } = [];
}
