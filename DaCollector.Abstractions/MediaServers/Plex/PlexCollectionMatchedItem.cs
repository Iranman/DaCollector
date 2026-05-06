using DaCollector.Abstractions.Collections;

namespace DaCollector.Abstractions.MediaServers.Plex;

/// <summary>
/// Pairing between a collection preview item and a Plex library item.
/// </summary>
public sealed record PlexCollectionMatchedItem
{
    /// <summary>
    /// Collection preview item.
    /// </summary>
    public CollectionBuilderPreviewItem Target { get; init; } = new();

    /// <summary>
    /// Plex library item matched by external ID.
    /// </summary>
    public PlexMediaItem PlexItem { get; init; } = new();
}
