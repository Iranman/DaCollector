namespace Shoko.Abstractions.MediaServers.Plex;

/// <summary>
/// Basic identity details returned by a Plex server.
/// </summary>
public sealed record PlexServerIdentity
{
    /// <summary>
    /// Base URL used for the Plex server request.
    /// </summary>
    public string BaseUrl { get; init; } = string.Empty;

    /// <summary>
    /// Whether The Collector could reach the server identity endpoint.
    /// </summary>
    public bool Reachable { get; init; }

    /// <summary>
    /// HTTP status code returned by Plex, when available.
    /// </summary>
    public int? StatusCode { get; init; }

    /// <summary>
    /// HTTP status text or connection error.
    /// </summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// Plex server machine identifier.
    /// </summary>
    public string? MachineIdentifier { get; init; }

    /// <summary>
    /// Plex server version.
    /// </summary>
    public string? Version { get; init; }

    /// <summary>
    /// Plex API version.
    /// </summary>
    public string? ApiVersion { get; init; }

    /// <summary>
    /// Whether the Plex server is claimed by a Plex account.
    /// </summary>
    public bool? Claimed { get; init; }
}
