using System.Collections.Generic;
using DaCollector.Abstractions.Metadata.Containers;
using DaCollector.Abstractions.Metadata.Tmdb.CrossReferences;

namespace DaCollector.Abstractions.Metadata.Tmdb;

/// <summary>
/// A TMDB episode.
/// </summary>
public interface ITmdbEpisode : IEpisode, IWithCreationDate, IWithUpdateDate
{
    /// <summary>
    /// The TMDB season id.
    /// </summary>
    string SeasonID { get; }

    /// <summary>
    /// The ordering ID.
    /// </summary>
    string OrderingID { get; }

    /// <summary>
    ///   The TvDB Episode ID for the TMDB episode ID, if known and available.
    /// </summary>
    int? TvdbEpisodeID { get; }

    /// <summary>
    /// Indicates the episode is hidden by the user.
    /// </summary>
    bool IsHidden { get; }

    /// <summary>
    /// Get the TMDB show info for the episode, if available.
    /// </summary>
    new ITmdbShow? Series { get; }

    /// <summary>
    /// Get the currently in use show ordering, if available.
    /// </summary>
    ITmdbShowOrderingInformation? SeriesOrdering { get; }

    /// <summary>
    /// Get the TMDB season info for the episode, if available.
    /// </summary>
    ITmdbSeason? Season { get; }

    /// <summary>
    /// Get the currently in use ordering.
    /// </summary>
    ITmdbEpisodeOrderingInformation Ordering { get; }

    /// <summary>
    /// The preferred ordering for the episode, if the episode is part of the
    /// preferred ordering for the show.
    /// </summary>
    ITmdbEpisodeOrderingInformation? PreferredOrdering { get; }

    /// <summary>
    /// The ordering information for the episode.
    /// </summary>
    IReadOnlyList<ITmdbEpisodeOrderingInformation> AllOrderings { get; }

    /// <summary>
    /// All DaCollector episode ↔ TMDB episode cross references linked to the TMDB episode.
    /// </summary>
    IReadOnlyList<ITmdbEpisodeCrossReference> TmdbEpisodeCrossReferences { get; }
}
