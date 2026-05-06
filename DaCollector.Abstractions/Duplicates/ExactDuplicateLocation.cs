using System;

namespace DaCollector.Abstractions.Duplicates;

/// <summary>
/// One stored file location that participates in an exact duplicate set.
/// </summary>
public sealed record ExactDuplicateLocation
{
    /// <summary>
    /// Internal video record ID.
    /// </summary>
    public int VideoID { get; init; }

    /// <summary>
    /// Internal file location ID.
    /// </summary>
    public int LocationID { get; init; }

    /// <summary>
    /// Managed folder ID containing this location.
    /// </summary>
    public int ManagedFolderID { get; init; }

    /// <summary>
    /// Managed folder display name.
    /// </summary>
    public string ManagedFolderName { get; init; } = string.Empty;

    /// <summary>
    /// Relative path inside the managed folder.
    /// </summary>
    public string RelativePath { get; init; } = string.Empty;

    /// <summary>
    /// Absolute path, when the managed folder can be resolved.
    /// </summary>
    public string? Path { get; init; }

    /// <summary>
    /// File name portion of the relative path.
    /// </summary>
    public string FileName { get; init; } = string.Empty;

    /// <summary>
    /// Whether the file currently exists at this location.
    /// </summary>
    public bool IsAvailable { get; init; }

    /// <summary>
    /// Whether the linked video record is ignored.
    /// </summary>
    public bool IsIgnored { get; init; }

    /// <summary>
    /// Whether this location is the suggested one to keep.
    /// </summary>
    public bool SuggestedKeep { get; init; }

    /// <summary>
    /// Whether this location is a suggested remove candidate.
    /// </summary>
    public bool SuggestedRemove { get; init; }

    /// <summary>
    /// When the linked video record was imported, if known.
    /// </summary>
    public DateTime? ImportedAt { get; init; }

    /// <summary>
    /// When the linked video record was created.
    /// </summary>
    public DateTime CreatedAt { get; init; }
}
