using System;
using System.Collections.Generic;
using DaCollector.Abstractions.Metadata.Anidb;
using DaCollector.Abstractions.Metadata.Anilist;
using DaCollector.Abstractions.Metadata.Anilist.CrossReferences;
using DaCollector.Abstractions.Metadata.Containers;
using DaCollector.Abstractions.Metadata.Tmdb;
using DaCollector.Abstractions.Metadata.Tmdb.CrossReferences;
using DaCollector.Abstractions.User;

namespace DaCollector.Abstractions.Metadata.DaCollector;

/// <summary>
/// DaCollector series metadata.
/// </summary>
public interface IDaCollectorSeries : ISeries, IWithCreationDate, IWithUpdateDate
{
    /// <summary>
    /// AniDB anime id linked to the DaCollector series.
    /// </summary>
    int AnidbAnimeID { get; }

    /// <summary>
    /// The id of the direct parent group of the DaCollector series.
    /// </summary>
    int ParentGroupID { get; }

    /// <summary>
    /// The id of the top-level parent group of the DaCollector series.
    /// </summary>
    int TopLevelGroupID { get; }

    /// <summary>
    /// All custom tags for the DaCollector series set by the user.
    /// </summary>
    IReadOnlyList<IDaCollectorTagForSeries> Tags { get; }

    /// <summary>
    ///   The number of missing normal episodes and specials for the DaCollector
    ///   series.
    /// </summary>
    int MissingEpisodeCount { get; }

    /// <summary>
    ///   The number of missing normal episodes and specials for the DaCollector
    ///   series which have been released by a release group we're collecting.
    /// </summary>
    int MissingCollectingEpisodeCount { get; }

    /// <summary>
    ///   The number of hidden missing normal episodes and specials for the
    ///   DaCollector series.
    /// </summary>
    int HiddenMissingEpisodeCount { get; }

    /// <summary>
    ///   The number of hidden missing normal episodes and specials for the
    ///   DaCollector series which have been released by a release group we're
    ///   collecting.
    /// </summary>
    int HiddenMissingCollectingEpisodeCount { get; }

    /// <summary>
    /// A direct link to the AniDB anime metadata.
    /// </summary>
    IAnidbAnime MetadataAnime { get; }

    /// <summary>
    ///   Wether or not AniList auto matching is disabled for the DaCollector series.
    /// </summary>
    bool AnilistAutoMatchingDisabled { get; }

    /// <summary>
    /// A direct link to all AniList anime linked to the DaCollector series.
    /// </summary>
    IReadOnlyList<IAnilistAnime> AnilistAnime { get; }

    /// <summary>
    /// All DaCollector series ↔ AniList anime cross references linked to the DaCollector series.
    /// </summary>
    IReadOnlyList<IAnilistAnimeCrossReference> AnilistAnimeCrossReferences { get; }

    /// <summary>
    ///   Wether or not TMDB auto matching is disabled for the DaCollector series.
    /// </summary>
    bool TmdbAutoMatchingDisabled { get; }

    /// <summary>
    /// A direct link to all TMDB shows linked to the DaCollector series.
    /// </summary>
    IReadOnlyList<ITmdbShow> TmdbShows { get; }

    /// <summary>
    /// A direct link to all TMDB seasons linked to the DaCollector series.
    /// </summary>
    IReadOnlyList<ITmdbSeason> TmdbSeasons { get; }

    /// <summary>
    /// A direct link to all TMDB movies linked to the DaCollector series.
    /// </summary>
    IReadOnlyList<ITmdbMovie> TmdbMovies { get; }

    /// <summary>
    /// All DaCollector series ↔ TMDB show cross references linked to the DaCollector series.
    /// </summary>
    IReadOnlyList<ITmdbShowCrossReference> TmdbShowCrossReferences { get; }

    /// <summary>
    /// All DaCollector series ↔ TMDB season cross references linked to the DaCollector series.
    /// </summary>
    IReadOnlyList<ITmdbSeasonCrossReference> TmdbSeasonCrossReferences { get; }

    /// <summary>
    /// All DaCollector episode ↔ TMDB episode cross references linked to the DaCollector series.
    /// </summary>
    IReadOnlyList<ITmdbEpisodeCrossReference> TmdbEpisodeCrossReferences { get; }

    /// <summary>
    /// All DaCollector episode ↔ TMDB movie cross references linked to the DaCollector series.
    /// </summary>
    IReadOnlyList<ITmdbMovieCrossReference> TmdbMovieCrossReferences { get; }

    /// <summary>
    /// All series linked to the DaCollector series.
    /// </summary>
    IReadOnlyList<ISeries> LinkedSeries { get; }

    /// <summary>
    /// All movies linked to the DaCollector series.
    /// </summary>
    IReadOnlyList<IMovie> LinkedMovies { get; }

    /// <summary>
    /// The direct parent group of the series.
    /// </summary>
    IDaCollectorGroup ParentGroup { get; }

    /// <summary>
    /// The top-level parent group of the series. It may or may not be the same
    /// as <see cref="ParentGroup"/> depending on how nested your group
    /// structure is.
    /// </summary>
    IDaCollectorGroup TopLevelGroup { get; }

    /// <summary>
    /// Get an enumerable for all parent groups, starting at the
    /// <see cref="ParentGroup"/> all the way up to the <see cref="TopLevelGroup"/>.
    /// </summary>
    IReadOnlyList<IDaCollectorGroup> AllParentGroups { get; }

    /// <summary>
    /// All known fake "seasons" for the DaCollector series.
    /// </summary>
    new IReadOnlyList<IDaCollectorSeason> Seasons { get; }

    /// <summary>
    /// All episodes for the DaCollector series.
    /// </summary>
    new IReadOnlyList<IDaCollectorEpisode> Episodes { get; }

    /// <summary>
    ///   Gets the user-specific data for the DaCollector series and user.
    /// </summary>
    /// <param name="user">
    ///   The user to get the data for.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when the <paramref name="user"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   Thrown when the <paramref name="user"/> is not stored in the database.
    /// </exception>
    /// <returns>
    ///   The user-specific data for the DaCollector series and user.
    /// </returns>
    ISeriesUserData GetUserData(IUser user);
}
