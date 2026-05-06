using System.Collections.Generic;
using DaCollector.Abstractions.Metadata.Containers;
using DaCollector.Abstractions.Metadata.Tmdb.CrossReferences;

namespace DaCollector.Abstractions.Metadata.Tmdb;

/// <summary>
/// A TMDB season.
/// </summary>
public interface ITmdbSeason : ISeason, IWithCreationDate, IWithUpdateDate
{
    /// <summary>
    /// The ordering ID.
    /// </summary>
    string OrderingID { get; }

    /// <summary>
    /// Get the currently in use show ordering, if available.
    /// </summary>
    ITmdbShowOrderingInformation? CurrentShowOrdering { get; }

    /// <summary>
    /// Get the TMDB show info for the season, if available.
    /// </summary>
    new ITmdbShow? Series { get; }

    /// <summary>
    /// All episodes for the TMDB season.
    /// </summary>
    new IReadOnlyList<ITmdbEpisode> Episodes { get; }

    /// <summary>
    /// All DaCollector series ↔ TMDB season cross references linked to the TMDB season.
    /// </summary>
    IReadOnlyList<ITmdbSeasonCrossReference> TmdbSeasonCrossReferences { get; }

    /// <summary>
    /// All DaCollector episode ↔ TMDB episode cross references linked to the TMDB season.
    /// </summary>
    IReadOnlyList<ITmdbEpisodeCrossReference> TmdbEpisodeCrossReferences { get; }
}
