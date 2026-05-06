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
/// DaCollector episode metadata.
/// </summary>
public interface IDaCollectorEpisode : IEpisode, IWithCreationDate, IWithUpdateDate
{
    /// <summary>
    /// The id of the anidb episode linked to the dacollector episode.
    /// </summary>
    int AnidbEpisodeID { get; }

    /// <summary>
    /// Indicates the episode is hidden by the user.
    /// </summary>
    bool IsHidden { get; }

    /// <summary>
    /// Get the dacollector series info for the episode, if available.
    /// </summary>
    new IDaCollectorSeries? Series { get; }

    /// <summary>
    /// A direct link to the anidb episode metadata.
    /// </summary>
    IAnidbEpisode AnidbEpisode { get; }

    /// <summary>
    /// A direct link to all anilist episodes linked to the dacollector episode.
    /// </summary>
    IReadOnlyList<IAnilistEpisode> AnilistEpisodes { get; }

    /// <summary>
    /// All DaCollector episode ↔ AniList episode cross references linked to the DaCollector episode.
    /// </summary>
    IReadOnlyList<IAnilistEpisodeCrossReference> AnilistEpisodeCrossReferences { get; }

    /// <summary>
    /// A direct link to all tmdb episodes linked to the dacollector episode.
    /// </summary>
    IReadOnlyList<ITmdbEpisode> TmdbEpisodes { get; }

    /// <summary>
    /// A direct link to all tmdb movies linked to the dacollector episode.
    /// </summary>
    IReadOnlyList<IMovie> TmdbMovies { get; }

    /// <summary>
    /// All DaCollector episode ↔ TMDB episode cross references linked to the DaCollector episode.
    /// </summary>
    IReadOnlyList<ITmdbEpisodeCrossReference> TmdbEpisodeCrossReferences { get; }

    /// <summary>
    /// All DaCollector episode ↔ TMDB movie cross references linked to the DaCollector episode.
    /// </summary>
    IReadOnlyList<ITmdbMovieCrossReference> TmdbMovieCrossReferences { get; }

    /// <summary>
    /// All episodes linked to this dacollector episode.
    /// </summary>
    IReadOnlyList<IEpisode> LinkedEpisodes { get; }

    /// <summary>
    /// All movies linked to this dacollector episode.
    /// </summary>
    IReadOnlyList<IMovie> LinkedMovies { get; }

    /// <summary>
    ///   Gets the user-specific data for the DaCollector episode and user.
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
    ///   The user-specific data for the DaCollector episode and user.
    /// </returns>
    IEpisodeUserData GetUserData(IUser user);
}
