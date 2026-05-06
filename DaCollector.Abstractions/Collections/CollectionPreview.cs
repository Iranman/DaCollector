using System.Collections.Generic;

namespace DaCollector.Abstractions.Collections;

/// <summary>
/// Preview result for a managed collection definition.
/// </summary>
public sealed record CollectionPreview
{
    /// <summary>
    /// Collection definition used for the preview.
    /// </summary>
    public CollectionDefinition Collection { get; init; } = new();

    /// <summary>
    /// Distinct media items resolved from all collection rules.
    /// </summary>
    public IReadOnlyList<CollectionBuilderPreviewItem> Items { get; init; } = [];

    /// <summary>
    /// Non-fatal issues encountered while evaluating the collection.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
