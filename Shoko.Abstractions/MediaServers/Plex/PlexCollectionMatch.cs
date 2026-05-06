using System.Collections.Generic;
using Shoko.Abstractions.Collections;

namespace Shoko.Abstractions.MediaServers.Plex;

/// <summary>
/// Plex match result for a managed collection preview.
/// </summary>
public sealed record PlexCollectionMatch
{
    /// <summary>
    /// Plex library section key used for matching.
    /// </summary>
    public string SectionKey { get; init; } = string.Empty;

    /// <summary>
    /// Items resolved in Plex.
    /// </summary>
    public IReadOnlyList<PlexCollectionMatchedItem> Matched { get; init; } = [];

    /// <summary>
    /// Collection preview items not found in Plex.
    /// </summary>
    public IReadOnlyList<CollectionBuilderPreviewItem> Missing { get; init; } = [];

    /// <summary>
    /// Total number of target items considered.
    /// </summary>
    public int TargetItemCount { get; init; }
}
