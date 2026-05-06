using DaCollector.Abstractions.Metadata.Containers;
using DaCollector.Abstractions.Metadata.DaCollector;

namespace DaCollector.Abstractions.Metadata.Tmdb.CrossReferences;

/// <summary>
///  A Cross-reference between an AniDB Anime and a TMDB Season.
/// </summary>
public interface ITmdbSeasonCrossReference : IWithImages
{
    /// <summary>
    ///   The AniDB Anime ID.
    /// </summary>
    int AnidbAnimeID { get; }

    /// <summary>
    ///   The TMDB Show ID for the TMDB Season.
    /// </summary>
    int TmdbShowID { get; }

    /// <summary>
    ///   The TMDB Season ID.
    /// </summary>
    string TmdbSeasonID { get; }

    /// <summary>
    ///   The season number of the TMDB Season within the TMDB Show.
    /// </summary>
    int SeasonNumber { get; }

    /// <summary>
    ///   The DaCollector Series for the cross-reference, if available.
    /// </summary>
    IDaCollectorSeries? DaCollectorSeries { get; }

    /// <summary>
    ///   The TMDB Show for the cross-reference, if available.
    /// </summary>
    ITmdbShow? TmdbShow { get; }

    /// <summary>
    ///   The TMDB Season for the cross-reference, if available.
    /// </summary>
    ITmdbSeason? TmdbSeason { get; }
}
