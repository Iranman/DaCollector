using System.Collections.Generic;
using DaCollector.Abstractions.Metadata;

namespace DaCollector.Abstractions.Duplicates;

/// <summary>
/// One Plex media entry that participates in a possible duplicate media set.
/// </summary>
public sealed record MediaDuplicateItem
{
    /// <summary>
    /// Plex rating key for the media entry.
    /// </summary>
    public string RatingKey { get; init; } = string.Empty;

    /// <summary>
    /// Plex title.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Plex media type, such as movie or show.
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
    /// External provider IDs attached to the Plex item.
    /// </summary>
    public IReadOnlyList<ExternalMediaId> ExternalIDs { get; init; } = [];

    /// <summary>
    /// Raw Plex GUID values.
    /// </summary>
    public IReadOnlyList<string> GuidValues { get; init; } = [];

    /// <summary>
    /// File paths exposed by Plex for this item.
    /// </summary>
    public IReadOnlyList<string> FilePaths { get; init; } = [];

    /// <summary>
    /// Stable hashes of file paths exposed by Plex.
    /// </summary>
    public IReadOnlyList<string> PathHashes { get; init; } = [];
}
