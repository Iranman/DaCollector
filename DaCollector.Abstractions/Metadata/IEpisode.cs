using System;
using System.Collections.Generic;
using DaCollector.Abstractions.Metadata.Enums;
using DaCollector.Abstractions.Metadata.Containers;
using DaCollector.Abstractions.Metadata.DaCollector;
using DaCollector.Abstractions.Video;

namespace DaCollector.Abstractions.Metadata;

/// <summary>
/// Episode metadata.
/// </summary>
public interface IEpisode : IWithTitles, IWithDescriptions, IWithImages, IWithCastAndCrew, IMetadata<int>
{
    /// <summary>
    /// The series id.
    /// </summary>
    int SeriesID { get; }

    /// <summary>
    /// The dacollector episode ID, if we have any.
    /// </summary>
    IReadOnlyList<int> DaCollectorEpisodeIDs { get; }

    /// <summary>
    /// The episode type.
    /// </summary>
    EpisodeType Type { get; }

    /// <summary>
    /// The episode number.
    /// </summary>
    int EpisodeNumber { get; }

    /// <summary>
    /// The season number, if applicable.
    /// </summary>
    int? SeasonNumber { get; }

    /// <summary>
    /// Overall user rating for the episode, normalized on a scale of 1-10.
    /// </summary>
    double Rating { get; }

    /// <summary>
    /// The number of votes which were used to calculate the rating.
    /// </summary>
    int RatingVotes { get; }

    /// <summary>
    /// The default thumbnail for the episode.
    /// </summary>
    IImage? DefaultThumbnail { get; }

    /// <summary>
    /// The runtime of the episode, as a time span.
    /// </summary>
    TimeSpan Runtime { get; }

    /// <summary>
    /// The day the episode aired, if available.
    /// </summary>
    DateOnly? AirDate { get; }

    /// <summary>
    ///   The precise day and time the episode aired, if available.
    /// </summary>
    DateTime? AirDateWithTime { get; }

    /// <summary>
    /// Get the series info for the episode, if available.
    /// </summary>
    ISeries? Series { get; }

    /// <summary>
    /// All dacollector episodes linked to this episode.
    /// </summary>
    IReadOnlyList<IDaCollectorEpisode> DaCollectorEpisodes { get; }

    /// <summary>
    /// All cross-references linked to the episode.
    /// </summary>
    IReadOnlyList<IVideoCrossReference> CrossReferences { get; }

    /// <summary>
    /// Get all videos linked to the episode, if any.
    /// </summary>
    IReadOnlyList<IVideo> VideoList { get; }
}
