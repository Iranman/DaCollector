namespace Shoko.Abstractions.Metadata.Enums;

/// <summary>
/// External metadata provider that can identify or describe a media item.
/// </summary>
public enum ExternalProvider : int
{
    /// <summary>
    /// Unknown or unresolved provider.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// AniDB.
    /// </summary>
    AniDB = 1,

    /// <summary>
    /// The Movie Database.
    /// </summary>
    TMDB = 2,

    /// <summary>
    /// IMDb.
    /// </summary>
    IMDb = 3,

    /// <summary>
    /// TheTVDB.
    /// </summary>
    TVDB = 4,

    /// <summary>
    /// Trakt.
    /// </summary>
    Trakt = 5,

    /// <summary>
    /// Plex.
    /// </summary>
    Plex = 6,

    /// <summary>
    /// Jellyfin.
    /// </summary>
    Jellyfin = 7,

    /// <summary>
    /// Manually assigned local identity.
    /// </summary>
    Manual = 8,
}
