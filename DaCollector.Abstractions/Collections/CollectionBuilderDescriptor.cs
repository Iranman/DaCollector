using DaCollector.Abstractions.Metadata.Enums;

namespace DaCollector.Abstractions.Collections;

/// <summary>
/// Describes one collection builder supported by DaCollector.
/// </summary>
public sealed record CollectionBuilderDescriptor
{
    /// <summary>
    /// Builder key used in collection rules.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Primary metadata provider used by the builder.
    /// </summary>
    public ExternalProvider Provider { get; init; } = ExternalProvider.Unknown;

    /// <summary>
    /// Media kind normally returned by the builder. Unknown means the builder can return multiple kinds.
    /// </summary>
    public MediaKind Kind { get; init; } = MediaKind.Unknown;

    /// <summary>
    /// Short user-facing description.
    /// </summary>
    public string Description { get; init; } = string.Empty;
}
