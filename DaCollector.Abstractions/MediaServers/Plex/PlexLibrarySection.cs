namespace DaCollector.Abstractions.MediaServers.Plex;

/// <summary>
/// One Plex library section.
/// </summary>
public sealed record PlexLibrarySection
{
    /// <summary>
    /// Plex library section key.
    /// </summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>
    /// Library title.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Library type, such as movie or show.
    /// </summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>
    /// Library scanner.
    /// </summary>
    public string? Scanner { get; init; }

    /// <summary>
    /// Library metadata agent.
    /// </summary>
    public string? Agent { get; init; }

    /// <summary>
    /// Library language.
    /// </summary>
    public string? Language { get; init; }
}
