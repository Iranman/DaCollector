using System.Collections.Generic;

namespace Shoko.Abstractions.Collections;

/// <summary>
/// Preview result for a collection builder rule.
/// </summary>
public sealed record CollectionBuilderPreview
{
    /// <summary>
    /// Builder descriptor used to produce the preview.
    /// </summary>
    public CollectionBuilderDescriptor Builder { get; init; } = new();

    /// <summary>
    /// Media items currently resolvable for the rule.
    /// </summary>
    public IReadOnlyList<CollectionBuilderPreviewItem> Items { get; init; } = [];

    /// <summary>
    /// Non-fatal issues encountered while evaluating the rule.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
