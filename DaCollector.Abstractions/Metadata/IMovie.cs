using System;
using System.Collections.Generic;
using DaCollector.Abstractions.Metadata.Containers;
using DaCollector.Abstractions.Metadata.DaCollector;
using DaCollector.Abstractions.Video;

namespace DaCollector.Abstractions.Metadata;

/// <summary>
/// Movie metadata.
/// </summary>
public interface IMovie : IWithTitles, IWithDescriptions, IWithImages, IWithCastAndCrew, IWithStudios, IWithContentRatings, IWithYearlySeasons, IMetadata<int>
{
    /// <summary>
    /// The dacollector series ID, if we have any.
    /// /// </summary>
    IReadOnlyList<int> DaCollectorSeriesIDs { get; }

    /// <summary>
    /// The dacollector episode ID, if we have any.
    /// /// </summary>
    IReadOnlyList<int> DaCollectorEpisodeIDs { get; }

    /// <summary>
    /// The first release date of the movie in the country of origin, if it's known.
    /// </summary>
    DateTime? ReleaseDate { get; }

    /// <summary>
    /// Indicates it's restricted for non-adult viewers. 😉
    /// </summary>
    bool Restricted { get; }

    /// <summary>
    /// Indicates that the entry is a standalone video, and not a movie.
    /// </summary>
    bool Video { get; }

    /// <summary>
    /// Overall user rating for the movie, normalized on a scale of 1-10.
    /// </summary>
    double Rating { get; }

    /// <summary>
    /// The number of votes which were used to calculate the rating.
    /// </summary>
    int RatingVotes { get; }

    /// <summary>
    /// Default poster for the movie.
    /// </summary>
    IImage? DefaultPoster { get; }

    /// <summary>
    /// All dacollector episodes linked to the movie.
    /// </summary>
    IReadOnlyList<IDaCollectorEpisode> DaCollectorEpisodes { get; }

    /// <summary>
    /// All dacollector series linked to the movie.
    /// </summary>
    IReadOnlyList<IDaCollectorSeries> DaCollectorSeries { get; }

    /// <summary>
    /// Related series.
    /// </summary>
    IReadOnlyList<IRelatedMetadata<IMovie, ISeries>> RelatedSeries { get; }

    /// <summary>
    /// Related movies.
    /// </summary>
    IReadOnlyList<IRelatedMetadata<IMovie, IMovie>> RelatedMovies { get; }

    /// <summary>
    /// All cross-references linked to the episode.
    /// </summary>
    IReadOnlyList<IVideoCrossReference> CrossReferences { get; }

    /// <summary>
    /// Get all videos linked to the series, if any.
    /// </summary>
    IReadOnlyList<IVideo> VideoList { get; }
}
