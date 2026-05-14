using System;
using System.Collections.Generic;

namespace DaCollector.Abstractions.Theme;

/// <summary>
/// Read-only view of an installed or preview theme, suitable for plugin consumption.
/// </summary>
public record ThemeInfo
{
    /// <summary>Unique identifier inferred from the theme file name.</summary>
    public required string ID { get; init; }

    /// <summary>Human-readable display name.</summary>
    public required string Name { get; init; }

    /// <summary>Optional short description of the theme.</summary>
    public string? Description { get; init; }

    /// <summary>Theme author name.</summary>
    public string? Author { get; init; }

    /// <summary>Author-defined tags for discovery.</summary>
    public required IReadOnlyList<string> Tags { get; init; }

    /// <summary>Theme version.</summary>
    public required Version Version { get; init; }

    /// <summary>URL for fetching theme updates, if provided by the theme author.</summary>
    public string? UpdateUrl { get; init; }

    /// <summary>URL for an external CSS overrides file, if provided by the theme author.</summary>
    public string? CssUrl { get; init; }

    /// <summary>Whether the theme is installed on disk.</summary>
    public required bool IsInstalled { get; init; }

    /// <summary>Whether this is only a preview (not yet persisted).</summary>
    public required bool IsPreview { get; init; }
}
