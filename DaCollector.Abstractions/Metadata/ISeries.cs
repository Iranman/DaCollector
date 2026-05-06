using System;
using System.Collections.Generic;
using DaCollector.Abstractions.Metadata.Enums;
using DaCollector.Abstractions.Metadata.Containers;
using DaCollector.Abstractions.Metadata.DaCollector;
using DaCollector.Abstractions.Video;

namespace DaCollector.Abstractions.Metadata;

/// <summary>
/// Series metadata.
/// </summary>
public interface ISeries : IWithTitles, IWithDescriptions, IWithImages, IWithCastAndCrew, IWithStudios, IWithContentRatings, IWithYearlySeasons, IMetadata<int>
{
    /// <summary>
    /// The dacollector series ID, if we have any.
    /// /// </summary>
    IReadOnlyList<int> DaCollectorSeriesIDs { get; }

    /// <summary>
    /// The Anime Type.
    /// </summary>
    AnimeType Type { get; }

    /// <summary>
    /// The first aired date, if known.
    /// </summary>
    /// <value></value>
    DateTime? AirDate { get; }

    /// <summary>
    /// The end date of the series. Null means that it's still airing.
    /// </summary>
    DateTime? EndDate { get; }

    /// <summary>
    /// Overall user rating for the show, normalized on a scale of 1-10.
    /// </summary>
    double Rating { get; }

    /// <summary>
    /// The number of votes which were used to calculate the rating.
    /// </summary>
    int RatingVotes { get; }

    /// <summary>
    /// Indicates it's restricted for non-adult viewers. 😉
    /// </summary>
    bool Restricted { get; }

    /// <summary>
    /// Default poster for the series.
    /// </summary>
    IImage? DefaultPoster { get; }

    /// <summary>
    /// All dacollector series linked to this entity.
    /// </summary>
    IReadOnlyList<IDaCollectorSeries> DaCollectorSeries { get; }

    /// <summary>
    /// Related series.
    /// </summary>
    IReadOnlyList<IRelatedMetadata<ISeries, ISeries>> RelatedSeries { get; }

    /// <summary>
    /// Related movies.
    /// </summary>
    IReadOnlyList<IRelatedMetadata<ISeries, IMovie>> RelatedMovies { get; }

    /// <summary>
    /// All cross-references linked to the series.
    /// </summary>
    IReadOnlyList<IVideoCrossReference> CrossReferences { get; }

    /// <summary>
    /// All known seasons for the series.
    /// </summary>
    IReadOnlyList<ISeason> Seasons { get; }

    /// <summary>
    /// All known episodes for the series.
    /// </summary>
    IReadOnlyList<IEpisode> Episodes { get; }

    /// <summary>
    /// Get all videos linked to the series, if any.
    /// </summary>
    IReadOnlyList<IVideo> Videos { get; }

    /// <summary>
    /// The number of total episodes in the series.
    /// </summary>
    EpisodeCounts EpisodeCounts { get; }
}
