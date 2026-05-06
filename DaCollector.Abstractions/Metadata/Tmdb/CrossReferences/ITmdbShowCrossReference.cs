using DaCollector.Abstractions.Metadata.Enums;
using DaCollector.Abstractions.Metadata.Containers;
using DaCollector.Abstractions.Metadata.DaCollector;

namespace DaCollector.Abstractions.Metadata.Tmdb.CrossReferences;

/// <summary>
///  A Cross-reference between an AniDB Anime and a TMDB Show.
/// </summary>
public interface ITmdbShowCrossReference : IWithImages
{
    /// <summary>
    ///   The AniDB Anime ID.
    /// </summary>
    int AnidbAnimeID { get; }

    /// <summary>
    ///   The TMDB Show ID.
    /// </summary>
    int TmdbShowID { get; }

    /// <summary>
    ///   The match rating for the cross-reference.
    /// </summary>
    MatchRating MatchRating { get; }

    /// <summary>
    ///   The DaCollector Series for the cross-reference, if available.
    /// </summary>
    IDaCollectorSeries? DaCollectorSeries { get; }

    /// <summary>
    ///   The TMDB Show for the cross-reference, if available.
    /// </summary>
    ITmdbShow? TmdbShow { get; }
}
