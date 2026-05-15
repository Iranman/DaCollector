using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DaCollector.Abstractions.Metadata;
using DaCollector.Abstractions.Metadata.Enums;
using DaCollector.Abstractions.Extensions;
using DaCollector.Server.API.Annotations;
using DaCollector.Server.API.v3.Helpers;
using DaCollector.Server.API.v3.Models.Common;
using DaCollector.Server.API.v3.Models.DaCollector;
using DaCollector.Server.Extensions;
using DaCollector.Server.Filters.Info;
using DaCollector.Server.Models.AniDB;
using AniDbApiModels = DaCollector.Server.API.v3.Models.AniDB;
using DaCollector.Server.Models.DaCollector;
using DaCollector.Server.Models.TMDB;
using DaCollector.Server.Models.TVDB;
using DaCollector.Server.Repositories;
using DaCollector.Server.Repositories.Cached;
using DaCollector.Server.Scheduling;
using DaCollector.Server.Services;
using DaCollector.Server.Settings;

namespace DaCollector.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize]
public class DashboardController : BaseController
{
    private readonly QueueHandler _queueHandler;
    private readonly MediaSeriesService _seriesService;
    private readonly MediaSeries_UserRepository _seriesUser;
    private readonly VideoLocal_UserRepository _vlUsers;

    /// <summary>
    /// Get the counters of various collection stats
    /// </summary>
    /// <returns></returns>
    [HttpGet("Stats")]
    public Dashboard.CollectionStats GetStats()
    {
        var allSeries = RepoFactory.MediaSeries.GetAll()
            .Where(a => User.AllowedSeries(a))
            .ToList();
        var groupCount = allSeries
            .DistinctBy(a => a.MediaGroupID)
            .Count();
        var episodeDict = allSeries
            .ToDictionary(s => s, s => s.AllAnimeEpisodes);
        var episodes = episodeDict.Values
            .SelectMany(episodeList => episodeList)
            .ToList();
        var files = episodes
            .SelectMany(a => a.VideoLocals)
            .DistinctBy(a => a.VideoLocalID)
            .ToList();
        var totalFileSize = files
            .Sum(a => a.FileSize);
        var watchedEpisodes = episodes
            .Where(a => a.GetUserRecord(User.JMMUserID)?.WatchedDate != null)
            .ToList();
        // Count local watched series in the user's collection.
        var watchedSeries = allSeries.Count((Func<MediaSeries, bool>)(series =>
        {
            var missingNormalEpisodesTotal = series.MissingEpisodeCount + series.HiddenMissingEpisodeCount;
            var anime = series.AniDB_Anime;

            int totalWatchableNormalEpisodes;
            if (anime != null)
            {
                // If the series doesn't have any episodes, then skip it.
                if (anime.EpisodeCountNormal == 0)
                    return false;

                // If all the normal episodes are still missing, then skip it.
                if (anime.EpisodeCountNormal == missingNormalEpisodesTotal)
                    return false;

                totalWatchableNormalEpisodes = anime.EpisodeCountNormal - missingNormalEpisodesTotal;
            }
            else
            {
                // TMDB-native series: use locally-present episodes as the baseline.
                totalWatchableNormalEpisodes = episodeDict[series].Count(e => e.VideoLocals.Count > 0);
                if (totalWatchableNormalEpisodes == 0)
                    return false;
            }

            // If we don't have a user record for the series, then skip it.
            var record = _seriesUser.GetByUserAndSeriesID(User.JMMUserID, series.MediaSeriesID);
            if (record == null)
                return false;

            // Check if we've watched more or equal to the number of watchable normal episodes.
            var count = episodeDict[series]
                .Count((Func<MediaEpisode, bool>)(episode =>
                    (episode.AniDB_Episode?.EpisodeType ?? EpisodeType.Episode) == EpisodeType.Episode &&
                    episode.GetUserRecord(User.JMMUserID)?.WatchedDate != null));
            return count >= totalWatchableNormalEpisodes;
        }));
        // Calculate watched hours for both local episodes and non-local episodes.
        var hoursWatched = Math.Round(
            (decimal)watchedEpisodes.Sum(a => a.VideoLocals.FirstOrDefault()?.DurationTimeSpan.TotalHours ?? ((IEpisode)a).Runtime.TotalHours),
            1, MidpointRounding.AwayFromZero);
        // We cache the video local here since it may be gone later if the files are actively being removed.
        var places = files
            .SelectMany(a => a.Places.Select(b => new { a.VideoLocalID, VideoLocal = a, Place = b }))
            .ToList();
        var duplicates = places
            .Where(a => !a.VideoLocal.IsVariation)
            .SelectMany(a => RepoFactory.CrossRef_File_Episode.GetByEd2k(a.VideoLocal.Hash))
            .GroupBy(a => a.EpisodeID)
            .Count(a => a.Count() > 1);
        var percentDuplicates = places.Count == 0
            ? 0
            : Math.Round((decimal)duplicates * 100 / places.Count, 2, MidpointRounding.AwayFromZero);
        var missingEpisodes = allSeries.Sum(a => a.MissingEpisodeCount);
        var missingEpisodesCollecting = allSeries.Sum(a => a.MissingEpisodeCountGroups);
        var multipleEpisodes = episodes.Count(a => a.VideoLocals.Count(b => !b.IsVariation) > 1);
        var unrecognizedFiles = RepoFactory.VideoLocal.GetVideosWithoutEpisodeUnsorted().Count;
        var duplicateFiles = places.GroupBy(a => a.VideoLocalID).Count(a => a.Count() > 1);
        var seriesWithMissingLinks = allSeries.Count(MissingTMDBLink);
        return new()
        {
            FileCount = files.Count,
            FileSize = totalFileSize,
            SeriesCount = allSeries.Count,
            GroupCount = groupCount,
            FinishedSeries = watchedSeries,
            WatchedEpisodes = watchedEpisodes.Count,
            WatchedHours = hoursWatched,
            PercentDuplicate = percentDuplicates,
            MissingEpisodes = missingEpisodes,
            MissingEpisodesCollecting = missingEpisodesCollecting,
            UnrecognizedFiles = unrecognizedFiles,
            SeriesWithMissingLinks = seriesWithMissingLinks,
            EpisodesWithMultipleFiles = multipleEpisodes,
            FilesWithDuplicateLocations = duplicateFiles
        };
    }

    private static bool MissingTMDBLink(MediaSeries ser)
    {
        if (ser.TMDB_MovieID.HasValue || ser.TMDB_ShowID.HasValue)
            return false;

        if (MissingTmdbLinkExpression.AnimeTypes.Contains(ser.AniDB_Anime?.MediaType ?? MediaType.Unknown))
            return false;

        if (ser.IsTMDBAutoMatchingDisabled)
            return false;

        var tmdbMovieLinkMissing = RepoFactory.CrossRef_AniDB_TMDB_Movie.GetByAnidbAnimeID(ser.AniDB_ID ?? 0).Count == 0;
        var tmdbShowLinkMissing = RepoFactory.CrossRef_AniDB_TMDB_Show.GetByAnidbAnimeID(ser.AniDB_ID ?? 0).Count == 0;
        return tmdbMovieLinkMissing && tmdbShowLinkMissing;
    }

    /// <summary>
    /// Gets the top number of the most common tags visible to the current user.
    /// </summary>
    /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
    /// <param name="page">Page number.</param>
    /// <param name="filter">The <see cref="TagFilter.Filter" /> to use. (Defaults to <see cref="TagFilter.Filter.AnidbInternal" /> | <see cref="TagFilter.Filter.Misc" /> | <see cref="TagFilter.Filter.Source" />)</param>
    /// <returns></returns>
    [HttpGet("TopTags")]
    public List<Tag> GetTopTags(
        [FromQuery, Range(0, 100)] int pageSize = 10,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery] TagFilter.Filter filter = TagFilter.Filter.AnidbInternal | TagFilter.Filter.Misc | TagFilter.Filter.Source
    )
    {
        var tags = RepoFactory.AniDB_Anime_Tag.GetAllForLocalSeries()
            .GroupBy(xref => xref.TagID)
            .Select(xrefList => (tag: RepoFactory.AniDB_Tag.GetByTagID(xrefList.Key), weight: xrefList.Count()))
            .Where(tuple => tuple.tag != null && User.AllowedTag(tuple.tag))
            .OrderByDescending(tuple => tuple.weight)
            .Select(tuple => new Tag(tuple.tag, true) { Weight = tuple.weight })
            .ToList();
        var tagDict = tags
            .ToDictionary(tag => tag.Name.ToLowerInvariant());
        var tagFilter = new TagFilter<Tag>(name => tagDict.TryGetValue(name.ToLowerInvariant(), out var tag)
            ? tag : null, tag => tag.Name, name => new Tag { Name = name, Weight = 0 });
        if (pageSize <= 0)
            return tagFilter
                .ProcessTags(filter, tags)
                .ToList();
        return tagFilter
            .ProcessTags(filter, tags)
            .Skip(pageSize * (page - 1))
            .Take(pageSize)
            .ToList();
    }

    /// <summary>
    /// Gets a breakdown of which types of anime the user has access to
    /// </summary>
    /// <returns></returns>
    [HttpGet("SeriesSummary")]
    public Dashboard.SeriesSummary GetSeriesSummary()
    {
        var series = RepoFactory.MediaSeries.GetAll()
            .Where(User.AllowedSeries)
            .GroupBy(a => a.AniDB_Anime?.MediaType ?? ((MediaType)0x42))
            .ToDictionary(a => a.Key, a => a.Count());

        return new Dashboard.SeriesSummary
        {
            Series = series.GetValueOrDefault(MediaType.TVSeries, 0),
            Special = series.GetValueOrDefault(MediaType.TVSpecial, 0),
            Movie = series.GetValueOrDefault(MediaType.Movie, 0),
            OVA = series.GetValueOrDefault(MediaType.OVA, 0),
            Web = series.GetValueOrDefault(MediaType.Web, 0),
            Other = series.GetValueOrDefault(MediaType.Other, 0),
            MusicVideo = series.GetValueOrDefault(MediaType.MusicVideo, 0),
            Unknown = series.GetValueOrDefault(MediaType.Unknown, 0),
            None = series.GetValueOrDefault((MediaType)0x42, 0),
        };
    }

    /// <summary>
    /// Get a list of recently added <see cref="Dashboard.Episode"/>.
    /// </summary>
    /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
    /// <param name="page">Page number.</param>
    /// <param name="includeRestricted">Include episodes from restricted (H) series.</param>
    /// <returns></returns>
    [HttpGet("RecentlyAddedEpisodes")]
    public ListResult<Dashboard.Episode> GetRecentlyAddedEpisodes(
        [FromQuery, Range(0, 1000)] int pageSize = 30,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery] IncludeOnlyFilter includeRestricted = IncludeOnlyFilter.False
    )
    {
        var user = HttpContext.GetUser();
        var episodeList = RepoFactory.VideoLocal.GetAll()
            .Where(f => f.DateTimeImported.HasValue)
            .OrderByDescending(f => f.DateTimeImported)
            .SelectMany<VideoLocal, (VideoLocal file, MediaEpisode? episode, MediaSeries? series)>(file =>
            {
                if (file.AnimeEpisodes.Count > 0)
                    return file.AnimeEpisodes.Select(ep => (file, (MediaEpisode?)ep, ep.MediaSeries));
                return GetTmdbNativeSeries(file.VideoLocalID)
                    .Select(s => (file, (MediaEpisode?)null, (MediaSeries?)s));
            });
        var seriesDict = episodeList
            .Select(tuple => tuple.episode?.MediaSeries ?? tuple.series)
            .WhereNotNull()
            .DistinctBy(series => series.MediaSeriesID)
            .Where(series => user.AllowedSeries(series))
            .ToDictionary(series => series.MediaSeriesID);
        return episodeList
            .Where(tuple =>
            {
                var seriesId = tuple.episode?.MediaSeriesID ?? tuple.series?.MediaSeriesID;
                if (seriesId is null || !seriesDict.TryGetValue(seriesId.Value, out var series))
                    return false;

                if (includeRestricted is not IncludeOnlyFilter.True)
                {
                    var onlyRestricted = includeRestricted is IncludeOnlyFilter.Only;
                    var isRestricted = series.AniDB_Anime?.IsRestricted ?? false;
                    if (onlyRestricted != isRestricted)
                        return false;
                }

                return true;
            })
            .ToListResult(
                tuple =>
                {
                    var series = tuple.episode?.MediaSeries ?? tuple.series!;
                    return tuple.episode is not null
                        ? GetEpisodeDetailsForSeriesAndEpisode(user, tuple.episode, series, file: tuple.file)
                        : GetTmdbNativeEpisodeDetails(user, tuple.file, series);
                },
                page,
                pageSize
            );
    }

    /// <summary>
    /// Get a list of recently added <see cref="Series"/>.
    /// </summary>
    /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
    /// <param name="page">Page number.</param>
    /// <param name="includeRestricted">Include restricted (H) series.</param>
    /// <returns></returns>
    [HttpGet("RecentlyAddedSeries")]
    public ListResult<Series> GetRecentlyAddedSeries(
        [FromQuery, Range(0, 1000)] int pageSize = 20,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery] IncludeOnlyFilter includeRestricted = IncludeOnlyFilter.False
    )
    {
        var user = HttpContext.GetUser();
        return RepoFactory.VideoLocal.GetAll()
            .Where(f => f.DateTimeImported.HasValue)
            .OrderByDescending(f => f.DateTimeImported)
            .SelectMany(file => file.AnimeEpisodes.Count > 0
                ? file.AnimeEpisodes.Select(ep => ep.MediaSeriesID)
                : GetTmdbNativeSeries(file.VideoLocalID).Select(s => s.MediaSeriesID))
            .Distinct()
            .Select(RepoFactory.MediaSeries.GetByID)
            .Where(series =>
            {
                if (series == null || !user.AllowedSeries(series))
                    return false;

                if (includeRestricted is not IncludeOnlyFilter.True)
                {
                    var onlyRestricted = includeRestricted is IncludeOnlyFilter.Only;
                    var isRestricted = series.AniDB_Anime?.IsRestricted ?? false;
                    if (onlyRestricted != isRestricted)
                        return false;
                }

                return true;
            })
            .ToListResult(a => new Series(a, User.JMMUserID), page, pageSize);
    }

    /// <summary>
    /// Get a list of the episodes to continue watching (soon-to-be) in recently watched order.
    /// </summary>
    /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
    /// <param name="page">Page number.</param>
    /// <param name="includeSpecials">Include specials in the search.</param>
    /// <param name="includeOthers">Include other type episodes in the search.</param>
    /// <param name="includeRestricted">Include episodes from restricted (H) series.</param>
    /// <returns></returns>
    [HttpGet("ContinueWatchingEpisodes")]
    public ListResult<Dashboard.Episode> GetContinueWatchingEpisodes(
        [FromQuery, Range(0, 100)] int pageSize = 20,
        [FromQuery, Range(0, int.MaxValue)] int page = 0,
        [FromQuery] bool includeSpecials = true,
        [FromQuery] bool includeOthers = false,
        [FromQuery] IncludeOnlyFilter includeRestricted = IncludeOnlyFilter.False
    )
    {
        var user = HttpContext.GetUser();
        return RepoFactory.MediaSeries_User.GetByUserID(user.JMMUserID)
            .Where(record => record.LastEpisodeUpdate.HasValue)
            .OrderByDescending(record => record.LastEpisodeUpdate)
            .Select(record => RepoFactory.MediaSeries.GetByID(record.MediaSeriesID))
            .Where(series =>
            {
                if (series == null || !user.AllowedSeries(series))
                    return false;

                if (includeRestricted is not IncludeOnlyFilter.True)
                {
                    var onlyRestricted = includeRestricted is IncludeOnlyFilter.Only;
                    var isRestricted = series.AniDB_Anime?.IsRestricted ?? false;
                    if (onlyRestricted != isRestricted)
                        return false;
                }

                return true;
            })
            .Select(series => (series, episode: _seriesService.GetActiveEpisode(series, user.JMMUserID, includeSpecials, includeOthers)))
            .Where(tuple => tuple.episode != null)
            .ToListResult(tuple => GetEpisodeDetailsForSeriesAndEpisode(user, tuple.episode, tuple.series), page, pageSize);
    }

    /// <summary>
    /// Get the next episodes for series that currently don't have an active watch session for the user.
    /// </summary>
    /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
    /// <param name="page">Page number.</param>
    /// <param name="onlyUnwatched">Only show unwatched episodes.</param>
    /// <param name="includeSpecials">Include specials in the search.</param>
    /// <param name="includeOthers">Include other type episodes in the search.</param>
    /// <param name="includeRestricted">Include episodes from restricted (H) series.</param>
    /// <param name="includeMissing">Include missing episodes in the list.</param>
    /// <param name="includeRewatching">Include already watched episodes in the
    /// search if we determine the user is "re-watching" the series.</param>
    /// <returns></returns>
    [HttpGet("NextUpEpisodes")]
    public ListResult<Dashboard.Episode> GetNextUpEpisodes(
        [FromQuery, Range(0, 100)] int pageSize = 20,
        [FromQuery, Range(0, int.MaxValue)] int page = 0,
        [FromQuery] bool onlyUnwatched = true,
        [FromQuery] bool includeSpecials = true,
        [FromQuery] bool includeOthers = false,
        [FromQuery] IncludeOnlyFilter includeRestricted = IncludeOnlyFilter.False,
        [FromQuery] bool includeMissing = false,
        [FromQuery] bool includeRewatching = false
    )
    {
        var user = HttpContext.GetUser();
        return RepoFactory.MediaSeries_User.GetByUserID(user.JMMUserID)
            .Where(record =>
                record.LastEpisodeUpdate.HasValue && (!onlyUnwatched || record.UnwatchedEpisodeCount > 0))
            .OrderByDescending(record => record.LastEpisodeUpdate)
            .Select(record => RepoFactory.MediaSeries.GetByID(record.MediaSeriesID))
            .Where(series =>
            {
                if (series == null || !user.AllowedSeries(series))
                    return false;

                if (includeRestricted is not IncludeOnlyFilter.True)
                {
                    var onlyRestricted = includeRestricted is IncludeOnlyFilter.Only;
                    var isRestricted = series.AniDB_Anime?.IsRestricted ?? false;
                    if (onlyRestricted != isRestricted)
                        return false;
                }

                return true;
            })
            .Select(series => (series, episode: _seriesService.GetNextUpEpisode(
                series,
                user.JMMUserID,
                new()
                {
                    DisableFirstEpisode = true,
                    IncludeCurrentlyWatching = !onlyUnwatched,
                    IncludeMissing = includeMissing,
                    IncludeRewatching = includeRewatching,
                    IncludeSpecials = includeSpecials,
                    IncludeOthers = includeOthers,
                }
            )))
            .Where(tuple => tuple.episode is not null)
            .ToListResult(tuple => GetEpisodeDetailsForSeriesAndEpisode(user, tuple.episode, tuple.series), page, pageSize);
    }

    [NonAction]
    public Dashboard.Episode GetEpisodeDetailsForSeriesAndEpisode(JMMUser user, MediaEpisode episode,
        MediaSeries series, AniDB_Anime? anime = null, VideoLocal? file = null)
    {
        VideoLocal_User? userRecord;
        var anidbEpisode = episode.AniDB_Episode;
        anime ??= series.AniDB_Anime;

        if (file is not null)
        {
            userRecord = _vlUsers.GetByUserAndVideoLocalID(user.JMMUserID, file.VideoLocalID);
        }
        else
        {
            (file, userRecord) = episode.VideoLocals
                .Select(f => (file: f, userRecord: _vlUsers.GetByUserAndVideoLocalID(user.JMMUserID, f.VideoLocalID)))
                .OrderByDescending(tuple => tuple.userRecord?.LastUpdated)
                .ThenByDescending(tuple => tuple.file.DateTimeCreated)
                .FirstOrDefault();
        }

        if (anidbEpisode != null && anime != null)
            return new Dashboard.Episode(anidbEpisode, anime, series, file, userRecord);

        return new Dashboard.Episode(episode, series, file, userRecord);
    }

    [NonAction]
    private Dashboard.Episode GetTmdbNativeEpisodeDetails(JMMUser user, VideoLocal file, MediaSeries series)
    {
        var userRecord = _vlUsers.GetByUserAndVideoLocalID(user.JMMUserID, file.VideoLocalID);

        if (series.TMDB_MovieID.HasValue)
        {
            var movie = RepoFactory.TMDB_Movie.GetByTmdbMovieID(series.TMDB_MovieID.Value);
            if (movie is not null)
                return new Dashboard.Episode(movie, series, file, userRecord);
        }

        if (series.TMDB_ShowID.HasValue)
        {
            var tmdbEp = RepoFactory.CrossRef_File_TmdbEpisode.GetByVideoLocalID(file.VideoLocalID)
                .Select(x => RepoFactory.TMDB_Episode.GetByTmdbEpisodeID(x.TmdbEpisodeID))
                .FirstOrDefault(e => e?.TmdbShowID == series.TMDB_ShowID.Value);
            if (tmdbEp is not null)
                return new Dashboard.Episode(tmdbEp, series, file, userRecord);
        }

        if (series.TvdbShowExternalID.HasValue)
        {
            var tvdbEp = RepoFactory.CrossRef_File_TvdbEpisode.GetByVideoLocalID(file.VideoLocalID)
                .Select(x => RepoFactory.TVDB_Episode.GetByTvdbEpisodeID(x.TvdbEpisodeID))
                .FirstOrDefault(e => e?.TvdbShowID == series.TvdbShowExternalID.Value);
            if (tvdbEp is not null)
                return new Dashboard.Episode(tvdbEp, series, file, userRecord);
        }

        return new Dashboard.Episode
        {
            IDs = new Dashboard.EpisodeDetailsIDs { ID = 0, Series = 0, DaCollectorFile = file.VideoLocalID, DaCollectorSeries = series.MediaSeriesID },
            Title = series.Title,
            Number = 1,
            Type = AniDbApiModels.EpisodeType.Episode,
            Duration = file.DurationTimeSpan,
            ResumePosition = userRecord?.ProgressPosition,
            Watched = userRecord?.WatchedDate?.ToUniversalTime(),
            SeriesTitle = series.Title,
            SeriesPoster = new Image(0, ImageEntityType.Poster, DataSource.DaCollector),
        };
    }

    private static IEnumerable<MediaSeries> GetTmdbNativeSeries(int videoLocalId)
    {
        foreach (var xref in RepoFactory.CrossRef_File_TmdbMovie.GetByVideoLocalID(videoLocalId))
        {
            var s = RepoFactory.MediaSeries.GetAll().FirstOrDefault(x => x.TMDB_MovieID == xref.TmdbMovieID);
            if (s is not null) yield return s;
        }
        foreach (var xref in RepoFactory.CrossRef_File_TmdbEpisode.GetByVideoLocalID(videoLocalId))
        {
            var ep = RepoFactory.TMDB_Episode.GetByTmdbEpisodeID(xref.TmdbEpisodeID);
            if (ep is null) continue;
            var s = RepoFactory.MediaSeries.GetAll().FirstOrDefault(x => x.TMDB_ShowID == ep.TmdbShowID);
            if (s is not null) yield return s;
        }
        foreach (var xref in RepoFactory.CrossRef_File_TvdbEpisode.GetByVideoLocalID(videoLocalId))
        {
            var ep = RepoFactory.TVDB_Episode.GetByTvdbEpisodeID(xref.TvdbEpisodeID);
            if (ep is null) continue;
            var s = RepoFactory.MediaSeries.GetAll().FirstOrDefault(x => x.TvdbShowExternalID == ep.TvdbShowID);
            if (s is not null) yield return s;
        }
    }

    /// <summary>
    /// Get the next <paramref name="numberOfDays"/> from the AniDB Calendar.
    /// </summary>
    /// <param name="numberOfDays">Number of days to show.</param>
    /// <param name="showAll">Show all series.</param>
    /// <param name="includeRestricted">Include episodes from restricted (H) series.</param>
    /// <returns></returns>
    [HttpGet("AniDBCalendar")]
    public List<Dashboard.Episode> GetAniDBCalendarInDays([FromQuery] int numberOfDays = 7,
        [FromQuery] bool showAll = false, [FromQuery] bool includeRestricted = false)
        => GetCalendarEpisodes(
            DateTime.Today.ToDateOnly(),
            DateTime.Today.ToDateOnly().AddDays(numberOfDays),
            showAll ? IncludeOnlyFilter.True : IncludeOnlyFilter.False,
            includeRestricted ? IncludeOnlyFilter.True : IncludeOnlyFilter.False
        );

    /// <summary>
    /// Get the episodes within the given time-frame on the calendar.
    /// </summary>
    /// <param name="startDate">Start date.</param>
    /// <param name="endDate">End date.</param>
    /// <param name="includeMissing">Include missing episodes.</param>
    /// <param name="includeRestricted">Include episodes from restricted (H) series.</param>
    /// <returns></returns>
    [HttpGet("CalendarEpisodes")]
    public List<Dashboard.Episode> GetCalendarEpisodes(
        [FromQuery] DateOnly startDate = default,
        [FromQuery] DateOnly endDate = default,
        [FromQuery] IncludeOnlyFilter includeMissing = IncludeOnlyFilter.False,
        [FromQuery] IncludeOnlyFilter includeRestricted = IncludeOnlyFilter.False
    )
    {
        var user = HttpContext.GetUser();
        var episodeList = RepoFactory.AniDB_Episode.GetForDate(startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), endDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Unspecified))
            .ToList();
        var animeDict = episodeList
            .Select(episode => RepoFactory.AniDB_Anime.GetByAnimeID(episode.AnimeID))
            .Distinct()
            .ToDictionary(anime => anime.AnimeID);
        var seriesDict = animeDict.Values
            .Select(anime => RepoFactory.MediaSeries.GetByAnimeID(anime.AnimeID))
            .WhereNotNull()
            .Distinct()
            .Where(anime => anime.AniDB_ID.HasValue)
            .ToDictionary(anime => anime.AniDB_ID!.Value);
        var anidbEntries = episodeList
            .Where(episode =>
            {
                if (!animeDict.TryGetValue(episode.AnimeID, out var anime) || !user.AllowedAnime(anime))
                    return false;

                if (includeRestricted is not IncludeOnlyFilter.True)
                {
                    var onlyRestricted = includeRestricted is IncludeOnlyFilter.Only;
                    var isRestricted = anime.IsRestricted;
                    if (onlyRestricted != isRestricted)
                        return false;
                }

                if (includeMissing is not IncludeOnlyFilter.True)
                {
                    var shouldHideMissing = includeMissing is IncludeOnlyFilter.False;
                    var isMissing = !seriesDict.ContainsKey(episode.AnimeID);
                    if (shouldHideMissing == isMissing)
                        return false;
                }

                return true;
            })
            .Select(episode =>
            {
                var anime = animeDict[episode.AnimeID];
                if (seriesDict.TryGetValue(episode.AnimeID, out var series))
                {
                    var xref = RepoFactory.CrossRef_File_Episode.GetByEpisodeID(episode.EpisodeID).MinBy(xref => xref.Percentage);
                    var file = xref?.VideoLocal;
                    return new Dashboard.Episode(episode, anime, series, file);
                }

                return new Dashboard.Episode(episode, anime);
            });

        var tmdbEntries = GetTmdbNativeCalendarEntries(startDate, endDate, includeMissing, includeRestricted, user);

        return anidbEntries.Concat(tmdbEntries)
            .OrderBy(ep => ep.AirDate)
            .ToList();
    }

    [NonAction]
    private IEnumerable<Dashboard.Episode> GetTmdbNativeCalendarEntries(
        DateOnly startDate, DateOnly endDate,
        IncludeOnlyFilter includeMissing, IncludeOnlyFilter includeRestricted,
        JMMUser? user)
    {
        // TMDB-native series are never restricted; exclude them when caller wants only restricted content
        if (includeRestricted is IncludeOnlyFilter.Only)
            yield break;

        var showIdToSeries = RepoFactory.MediaSeries.GetAll()
            .Where(s => s.TMDB_ShowID.HasValue)
            .ToDictionary(s => s.TMDB_ShowID!.Value, s => s);
        var movieIdToSeries = RepoFactory.MediaSeries.GetAll()
            .Where(s => s.TMDB_MovieID.HasValue)
            .ToDictionary(s => s.TMDB_MovieID!.Value, s => s);

        var userID = user?.JMMUserID ?? 0;

        // TMDB show episodes
        foreach (var ep in RepoFactory.TMDB_Episode.GetAll()
            .Where(e => e.SeasonNumber > 0 && e.AiredAt.HasValue
                        && e.AiredAt.Value >= startDate && e.AiredAt.Value <= endDate))
        {
            showIdToSeries.TryGetValue(ep.TmdbShowID, out var series);
            var isMissing = series == null;

            if (includeMissing is not IncludeOnlyFilter.True)
            {
                if ((includeMissing is IncludeOnlyFilter.False) == isMissing)
                    continue;
            }

            if (series == null) continue;
            if (user != null && !user.AllowedSeries(series)) continue;

            var xref = RepoFactory.CrossRef_File_TmdbEpisode.GetByTmdbEpisodeID(ep.TmdbEpisodeID).FirstOrDefault();
            var file = xref != null ? RepoFactory.VideoLocal.GetByID(xref.VideoLocalID) : null;
            var userRecord = file != null ? _vlUsers.GetByUserAndVideoLocalID(userID, file.VideoLocalID) : null;

            yield return new Dashboard.Episode(ep, series, file, userRecord);
        }

        // TMDB movies
        foreach (var movie in RepoFactory.TMDB_Movie.GetAll()
            .Where(m => m.ReleasedAt.HasValue
                        && m.ReleasedAt.Value >= startDate && m.ReleasedAt.Value <= endDate))
        {
            movieIdToSeries.TryGetValue(movie.TmdbMovieID, out var series);
            var isMissing = series == null;

            if (includeMissing is not IncludeOnlyFilter.True)
            {
                if ((includeMissing is IncludeOnlyFilter.False) == isMissing)
                    continue;
            }

            if (series != null && user != null && !user.AllowedSeries(series))
                continue;

            var xref = RepoFactory.CrossRef_File_TmdbMovie.GetByTmdbMovieID(movie.TmdbMovieID).FirstOrDefault();
            var file = xref != null ? RepoFactory.VideoLocal.GetByID(xref.VideoLocalID) : null;
            var userRecord = file != null ? _vlUsers.GetByUserAndVideoLocalID(userID, file.VideoLocalID) : null;

            if (series != null)
                yield return new Dashboard.Episode(movie, series, file, userRecord);
        }
    }

    public DashboardController(ISettingsProvider settingsProvider, QueueHandler queueHandler, MediaSeriesService seriesService, MediaSeries_UserRepository seriesUser, VideoLocal_UserRepository vlUsers) : base(settingsProvider)
    {
        _queueHandler = queueHandler;
        _seriesService = seriesService;
        _seriesUser = seriesUser;
        _vlUsers = vlUsers;
    }
}
