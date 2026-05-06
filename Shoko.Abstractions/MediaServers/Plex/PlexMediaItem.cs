using System.Collections.Generic;
using Shoko.Abstractions.Metadata;

namespace Shoko.Abstractions.MediaServers.Plex;

/// <summary>
/// One movie or TV item from a Plex library section.
/// </summary>
public sealed record PlexMediaItem
{
    /// <summary>
    /// Plex item rating key.
    /// </summary>
    public string RatingKey { get; init; } = string.Empty;

    /// <summary>
    /// Plex item title.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Plex item type, such as movie or show.
    /// </summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>
    /// Release or first-aired year, when available.
    /// </summary>
    public int? Year { get; init; }

    /// <summary>
    /// Primary Plex GUID.
    /// </summary>
    public string? Guid { get; init; }

    /// <summary>
    /// External provider identities exposed by Plex.
    /// </summary>
    public IReadOnlyList<ExternalMediaId> ExternalIDs { get; init; } = [];

    /// <summary>
    /// Raw Plex GUID values.
    /// </summary>
    public IReadOnlyList<string> GuidValues { get; init; } = [];
}
