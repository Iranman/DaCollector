using DaCollector.Abstractions.Metadata.Enums;
using DaCollector.Abstractions.Metadata.Containers;
using DaCollector.Abstractions.Metadata.DaCollector;

namespace DaCollector.Abstractions.Metadata.Tmdb.CrossReferences;

/// <summary>
///  A Cross-reference between an AniDB Episode and a TMDB Movie.
/// </summary>
public interface ITmdbMovieCrossReference : IWithImages
{
    /// <summary>
    ///   The AniDB Anime ID for the AniDB Episode.
    /// </summary>
    int AnidbAnimeID { get; }

    /// <summary>
    ///   The AniDB Episode ID.
    /// </summary>
    int AnidbEpisodeID { get; }

    /// <summary>
    ///   The TMDB Movie ID.
    /// </summary>
    int TmdbMovieID { get; }

    /// <summary>
    ///   The match rating for the cross-reference.
    /// </summary>
    MatchRating MatchRating { get; }

    /// <summary>
    ///   The DaCollector Series for the cross-reference, if available.
    /// </summary>
    IDaCollectorSeries? DaCollectorSeries { get; }

    /// <summary>
    ///   The DaCollector Episode for the cross-reference, if available.
    /// </summary>
    IDaCollectorEpisode? DaCollectorEpisode { get; }

    /// <summary>
    ///   The TMDB Movie for the cross-reference, if available.
    /// </summary>
    ITmdbMovie? TmdbMovie { get; }
}
