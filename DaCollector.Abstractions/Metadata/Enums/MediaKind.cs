namespace DaCollector.Abstractions.Metadata.Enums;

/// <summary>
/// Generic media type used by movie and TV collection management features.
/// </summary>
public enum MediaKind : int
{
    /// <summary>
    /// Unknown or unresolved media kind.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// A movie title.
    /// </summary>
    Movie = 1,

    /// <summary>
    /// A TV show title.
    /// </summary>
    Show = 2,

    /// <summary>
    /// A TV season.
    /// </summary>
    Season = 3,

    /// <summary>
    /// A TV episode.
    /// </summary>
    Episode = 4,

    /// <summary>
    /// A provider-managed collection, such as a TMDB movie collection.
    /// </summary>
    Collection = 5,

    /// <summary>
    /// A person, such as cast or crew.
    /// </summary>
    Person = 6,

    /// <summary>
    /// A company or studio.
    /// </summary>
    Company = 7,

    /// <summary>
    /// A TV network.
    /// </summary>
    Network = 8,
}
