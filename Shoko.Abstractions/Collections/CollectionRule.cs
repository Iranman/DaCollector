using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Collections;

/// <summary>
/// One provider-backed rule used to build or update a managed collection.
/// </summary>
public sealed record CollectionRule
{
    /// <summary>
    /// Builder name, such as <c>tmdb_popular</c>, <c>imdb_chart</c>, or <c>tvdb_show</c>.
    /// </summary>
    public string Builder { get; init; } = string.Empty;

    /// <summary>
    /// Media kind this rule is expected to return.
    /// </summary>
    public MediaKind Kind { get; init; } = MediaKind.Unknown;

    /// <summary>
    /// Provider-specific rule options.
    /// </summary>
    public IReadOnlyDictionary<string, string> Options { get; init; } = new Dictionary<string, string>();
}
