using System.Collections.Generic;
using DaCollector.Abstractions.Metadata.Containers;

namespace DaCollector.Abstractions.Metadata;

/// <summary>
/// Season Metadata.
/// </summary>
public interface ISeason : IWithTitles, IWithDescriptions, IWithImages, IWithCastAndCrew, IWithYearlySeasons, IMetadata<string>
{
    /// <summary>
    /// The TMDB show ID.
    /// </summary>
    int SeriesID { get; }

    /// <summary>
    /// Season number for default ordering.
    /// </summary>
    int SeasonNumber { get; }

    /// <summary>
    /// Default poster for the season.
    /// </summary>
    IImage? DefaultPoster { get; }

    /// <summary>
    /// Get the series info for the season, if available.
    /// </summary>
    ISeries? Series { get; }

    /// <summary>
    /// All episodes for the season.
    /// </summary>
    IReadOnlyList<IEpisode> Episodes { get; }
}
