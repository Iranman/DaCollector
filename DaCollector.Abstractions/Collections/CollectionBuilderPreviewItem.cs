using DaCollector.Abstractions.Metadata;

namespace DaCollector.Abstractions.Collections;

/// <summary>
/// One media item returned by a collection builder preview.
/// </summary>
public sealed record CollectionBuilderPreviewItem
{
    /// <summary>
    /// Stable provider identity for the preview item.
    /// </summary>
    public ExternalMediaId ExternalID { get; init; }

    /// <summary>
    /// Display title, when known.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Short provider summary, when available.
    /// </summary>
    public string? Summary { get; init; }
}
