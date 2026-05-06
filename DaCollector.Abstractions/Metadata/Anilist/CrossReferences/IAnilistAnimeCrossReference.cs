using DaCollector.Abstractions.Metadata.Enums;
using DaCollector.Abstractions.Metadata.Containers;
using DaCollector.Abstractions.Metadata.DaCollector;

namespace DaCollector.Abstractions.Metadata.Anilist.CrossReferences;

/// <summary>
///  A Cross-reference between an AniDB Anime and a AniList Anime.
/// </summary>
public interface IAnilistAnimeCrossReference : IWithImages
{
    /// <summary>
    ///   The AniDB Anime ID.
    /// </summary>
    int AnidbAnimeID { get; }

    /// <summary>
    ///   The AniList Anime ID.
    /// </summary>
    int AnilistAnimeID { get; }

    /// <summary>
    ///   The match rating for the cross-reference.
    /// </summary>
    MatchRating MatchRating { get; }

    /// <summary>
    ///   The DaCollector Series for the cross-reference, if available.
    /// </summary>
    IDaCollectorSeries? DaCollectorSeries { get; }

    /// <summary>
    ///   The AniList Anime for the cross-reference, if available.
    /// </summary>
    IAnilistAnime? AnilistAnime { get; }
}
