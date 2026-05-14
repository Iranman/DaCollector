using System;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using DaCollector.Abstractions.Metadata;
using MetaEnums = DaCollector.Abstractions.Metadata.Enums;
using DaCollector.Server.API.v3.Helpers;
using DaCollector.Server.API.v3.Models.AniDB;
using DaCollector.Server.API.v3.Models.Common;
using DaCollector.Server.Extensions;
using DaCollector.Server.Models.AniDB;
using DaCollector.Server.Models.DaCollector;
using DaCollector.Server.Repositories;

#nullable enable
namespace DaCollector.Server.API.v3.Models.DaCollector;

public static class Dashboard
{
    public class CollectionStats
    {
        /// <summary>
        /// Number of Files in the collection (visible to the current user)
        /// </summary>
        [Required]
        public int FileCount { get; set; }

        /// <summary>
        /// Number of Series in the Collection (visible to the current user)
        /// </summary>
        [Required]
        public int SeriesCount { get; set; }

        /// <summary>
        /// The number of Groups in the Collection (visible to the current user)
        /// </summary>
        [Required]
        public int GroupCount { get; set; }

        /// <summary>
        /// Total amount of space the collection takes (of what's visible to the current user)
        /// </summary>
        [Required]
        public long FileSize { get; set; }

        /// <summary>
        /// Number of Series Completely Watched
        /// </summary>
        [Required]
        public int FinishedSeries { get; set; }

        /// <summary>
        /// Number of Episodes Watched
        /// </summary>
        [Required]
        public int WatchedEpisodes { get; set; }

        /// <summary>
        /// Watched Hours, rounded to one place
        /// </summary>
        [Required]
        public decimal WatchedHours { get; set; }

        /// <summary>
        /// The percentage of files that are either duplicates or belong to the same episode
        /// </summary>
        [Required]
        public decimal PercentDuplicate { get; set; }

        /// <summary>
        /// The Number of missing episodes, regardless of where they are from or available
        /// </summary>
        [Required]
        public int MissingEpisodes { get; set; }

        /// <summary>
        /// The number of missing episodes from groups we are collecting. This should not be used as a rule, as it's not very reliable
        /// </summary>
        [Required]
        public int MissingEpisodesCollecting { get; set; }

        /// <summary>
        /// Number of Unrecognized Files
        /// </summary>
        [Required]
        public int UnrecognizedFiles { get; set; }

        /// <summary>
        /// The number of series missing TMDB Links
        /// </summary>
        [Required]
        public int SeriesWithMissingLinks { get; set; }

        /// <summary>
        /// The number of Episodes with more than one File (not marked as a variation)
        /// </summary>
        [Required]
        public int EpisodesWithMultipleFiles { get; set; }

        /// <summary>
        /// The number of files that exist in more than one location
        /// </summary>
        [Required]
        public int FilesWithDuplicateLocations { get; set; }
    }

    public class SeriesSummary
    {
        /// <summary>
        /// The number of normal Series
        /// </summary>
        [Required]
        public int Series { get; set; }

        /// <summary>
        /// The Number of OVAs
        /// </summary>
        [Required]
        public int OVA { get; set; }

        /// <summary>
        /// The Number of Movies
        /// </summary>
        [Required]
        public int Movie { get; set; }

        /// <summary>
        /// The Number of TV Specials
        /// </summary>
        [Required]
        public int Special { get; set; }

        /// <summary>
        /// ONAs and the like, it's more of a new concept
        /// </summary>
        [Required]
        public int Web { get; set; }

        /// <summary>
        /// Entries marked as Other, different from None.
        /// </summary>
        [Required]
        public int Other { get; set; }

        /// <summary>
        /// The Number of Music Videos
        /// </summary>
        [Required]
        public int MusicVideo { get; set; }

        /// <summary>
        /// The entry have not yet been assigned a type.
        /// </summary>
        [Required]
        public int Unknown { get; set; }

        /// <summary>
        /// Series that do not have metadata records. This usually means there was an error in the import process, or the API was hit while records were still being created.
        /// </summary>
        [Required]
        public int None { get; set; }
    }

    /// <summary>
    /// Episode details for displaying on the dashboard.
    /// </summary>
    public class Episode
    {
        public Episode(AniDB_Episode episode, AniDB_Anime anime, MediaSeries? series = null,
            VideoLocal? file = null, VideoLocal_User? userRecord = null)
        {
            IDs = new EpisodeDetailsIDs()
            {
                ID = episode.EpisodeID,
                Series = anime.AnimeID,
                DaCollectorFile = file?.VideoLocalID,
                DaCollectorSeries = series?.MediaSeriesID,
                DaCollectorEpisode = series != null
                    ? RepoFactory.MediaEpisode.GetByAniDBEpisodeID(episode.EpisodeID)?.MediaEpisodeID
                    : null
            };
            Title = episode.Title;
            Number = episode.EpisodeNumber;
            Type = episode.EpisodeType.ToV3Dto();
            AirDate = episode.GetAirDateAsDate()?.ToDateOnly();
            Duration = file?.DurationTimeSpan ?? new TimeSpan(0, 0, episode.LengthSeconds);
            ResumePosition = userRecord?.ProgressPosition;
            Watched = userRecord?.WatchedDate?.ToUniversalTime();
            SeriesTitle = series?.Title ?? anime.Title;
            SeriesPoster = new Image(anime.PreferredOrDefaultPoster);
            Thumbnail = episode.PreferredOrDefaultThumbnail is { } image ? new Image(image) : null;
        }

        public Episode(MediaEpisode episode, MediaSeries series, VideoLocal? file = null, VideoLocal_User? userRecord = null)
        {
            var iEpisode = (IEpisode)episode;
            IDs = new EpisodeDetailsIDs
            {
                ID = episode.MediaEpisodeID,
                Series = series.AniDB_ID ?? 0,
                DaCollectorFile = file?.VideoLocalID,
                DaCollectorSeries = series.MediaSeriesID,
                DaCollectorEpisode = episode.MediaEpisodeID,
            };
            Title = episode.Title;
            Number = iEpisode.EpisodeNumber;
            Type = episode.EpisodeType.ToV3Dto();
            AirDate = iEpisode.AirDate;
            Duration = file?.DurationTimeSpan ?? iEpisode.Runtime;
            ResumePosition = userRecord?.ProgressPosition;
            Watched = userRecord?.WatchedDate?.ToUniversalTime();
            SeriesTitle = series.Title;
            SeriesPoster = series.GetPreferredImageForType(MetaEnums.ImageEntityType.Poster) is { } poster
                ? new Image(poster)
                : new Image(0, MetaEnums.ImageEntityType.Poster, MetaEnums.DataSource.DaCollector);
            Thumbnail = episode.GetPreferredImageForType(MetaEnums.ImageEntityType.Thumbnail) is { } thumb
                ? new Image(thumb)
                : null;
        }

        /// <summary>
        /// All ids that may be useful for navigating away from the dashboard.
        /// </summary>
        [Required]
        public EpisodeDetailsIDs IDs { get; set; }

        /// <summary>
        /// Episode title.
        /// </summary>
        [Required]
        public string Title { get; set; }

        /// <summary>
        /// Episode number.
        /// </summary>
        [Required]
        public int Number { get; set; }

        /// <summary>
        /// Episode type.
        /// </summary>
        [Required, JsonConverter(typeof(StringEnumConverter))]
        public EpisodeType Type { get; set; }

        /// <summary>
        /// Air Date.
        /// </summary>
        /// <value></value>
        public DateOnly? AirDate { get; set; }

        /// <summary>
        /// The duration of the episode.
        /// </summary>
        [Required]
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Where to resume the next playback.
        /// </summary>
        public TimeSpan? ResumePosition { get; set; }

        /// <summary>
        /// If the file/episode is considered watched.
        /// </summary>
        [JsonConverter(typeof(IsoDateTimeConverter))]
        public DateTime? Watched { get; set; }

        /// <summary>
        /// Series title.
        /// </summary>
        [Required]
        public string SeriesTitle { get; set; }

        /// <summary>
        /// Series poster.
        /// </summary>
        [Required]
        public Image SeriesPoster { get; set; }

        /// <summary>
        /// Episode thumbnail.
        /// </summary>
        public Image? Thumbnail { get; set; }
    }

    /// <summary>
    /// Object holding ids related to the episode.
    /// </summary>
    public class EpisodeDetailsIDs : IDs
    {
        /// <summary>
        /// The related <see cref="MetadataEpisode"/> id for the entry.
        /// </summary>
        public new int ID { get; set; }

        /// <summary>
        /// The related source series id for the entry.
        /// </summary>
        [Required]
        public int Series { get; set; }

        /// <summary>
        /// The related DaCollector <see cref="File"/> id if a file is available
        /// and/or appropriate.
        /// </summary>
        public int? DaCollectorFile { get; set; }

        /// <summary>
        /// The related DaCollector <see cref="DaCollector.Episode"/> id if the episode is
        /// available locally.
        /// </summary>
        public int? DaCollectorEpisode { get; set; }

        /// <summary>
        /// The related DaCollector <see cref="DaCollector.Series"/> id if the series is
        /// available locally.
        /// </summary>
        public int? DaCollectorSeries { get; set; }
    }
}
