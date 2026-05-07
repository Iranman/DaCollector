namespace DaCollector.Abstractions.Duplicates;

/// <summary>
/// Result of previewing or applying a guarded exact duplicate deletion.
/// </summary>
public sealed record ExactDuplicateDeleteResult
{
    /// <summary>
    /// Whether this response is a dry run and no delete was attempted.
    /// </summary>
    public bool DryRun { get; init; } = true;

    /// <summary>
    /// Whether the duplicate candidate was deleted.
    /// </summary>
    public bool Deleted { get; init; }

    /// <summary>
    /// Whether DaCollector attempted to delete the physical file.
    /// </summary>
    public bool DeleteFile { get; init; }

    /// <summary>
    /// Whether DaCollector attempted to remove empty parent folders.
    /// </summary>
    public bool DeleteEmptyFolders { get; init; }

    /// <summary>
    /// Human-readable operation result.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Location selected for deletion.
    /// </summary>
    public ExactDuplicateLocation Location { get; init; } = new();

    /// <summary>
    /// Cleanup plan that authorized this delete candidate.
    /// </summary>
    public ExactDuplicateCleanupPlan Plan { get; init; } = new();

    /// <summary>
    /// Bytes expected to be reclaimed if the physical file exists and is removed.
    /// </summary>
    public long PotentialReclaimBytes { get; init; }
}
