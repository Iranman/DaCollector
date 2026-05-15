using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DaCollector.Abstractions.Extensions;
using DaCollector.Server.API.Annotations;
using DaCollector.Server.API.ModelBinders;
using DaCollector.Server.API.v3.Helpers;
using DaCollector.Server.API.v3.Models.Common;
using DaCollector.Server.API.v3.Models.DaCollector;
using DaCollector.Server.Extensions;
using DaCollector.Server.Models.DaCollector;
using DaCollector.Server.Models.TMDB;
using DaCollector.Server.Repositories;
using DaCollector.Server.Settings;

#pragma warning disable CA1822
namespace DaCollector.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/ReleaseManagement/DuplicateFiles")]
[ApiV3]
[Authorize]
public class ReleaseManagementDuplicateFilesController(ISettingsProvider settingsProvider) : BaseController(settingsProvider)
{
    /// <summary>
    /// Get episodes with duplicate files, with only the files with duplicates for each episode.
    /// </summary>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSourceType"/>s.</param>
    /// <param name="includeMediaInfo">Include media info data.</param>
    /// <param name="includeXRefs">Include file/episode cross-references with the episodes.</param>
    /// <param name="includeReleaseInfo">Include release info data.</param>
    /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
    /// <param name="page">Page number.</param>
    /// <returns></returns>
    [HttpGet("Episodes")]
    public ActionResult<ListResult<Episode>> GetEpisodes(
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSourceType> includeDataFrom = null,
        [FromQuery] bool includeMediaInfo = true,
        [FromQuery] bool includeXRefs = false,
        [FromQuery] bool includeReleaseInfo = false,
        [FromQuery, Range(0, 1000)] int pageSize = 100,
        [FromQuery, Range(1, int.MaxValue)] int page = 1)
    {
        var anidbEpisodes = RepoFactory.MediaEpisode.GetWithDuplicateFiles()
            .Select(episode =>
            {
                var duplicateFiles = DuplicateFilesFor(episode.VideoLocals);
                var dto = new Episode(HttpContext, episode, includeDataFrom);
                dto.Size = duplicateFiles.Count;
                dto.Files = duplicateFiles.Select(f => new File(HttpContext, f, includeXRefs, includeReleaseInfo, includeMediaInfo, true)).ToList();
                return dto;
            });

        var tmdbEpisodes = GetTmdbNativeDuplicateEpisodes(includeDataFrom, includeXRefs, includeReleaseInfo, includeMediaInfo);

        return anidbEpisodes.Concat(tmdbEpisodes).ToListResult(page, pageSize);
    }

    /// <summary>
    /// Get the list of file location ids to auto remove across all series.
    /// </summary>
    /// <returns></returns>
    [HttpGet("FileLocationsToAutoRemove")]
    public ActionResult<List<FileIdSet>> GetFileIdsWithPreference()
    {
        var enumerable = RepoFactory.MediaEpisode.GetWithDuplicateFiles();

        return enumerable
            .SelectMany(episode =>
                episode.VideoLocals
                    .SelectMany(a => a.Places.ExceptBy((a.FirstValidPlace ?? a.FirstResolvedPlace) is { } fileLocation ? [fileLocation.ID] : [], b => b.ID))
                    .Select(file => (episode.MediaSeriesID, episode.MediaEpisodeID, file.VideoID, file.ID))
            )
            .GroupBy(tuple => tuple.VideoID, tuple => (tuple.ID, tuple.MediaEpisodeID, tuple.MediaSeriesID))
            .Select(groupBy => new FileIdSet(groupBy))
            .ToList();
    }

    /// <summary>
    /// Get series with duplicate files.
    /// </summary>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSourceType"/>s.</param>
    /// <param name="onlyFinishedSeries">Only show finished series.</param>
    /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
    /// <param name="page">Page number.</param>
    /// <returns></returns>
    [HttpGet("Series")]
    public ActionResult<ListResult<Series.WithEpisodeCount>> GetSeriesWithDuplicateFiles(
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSourceType> includeDataFrom = null,
        [FromQuery] bool onlyFinishedSeries = false,
        [FromQuery, Range(0, 1000)] int pageSize = 100,
        [FromQuery, Range(1, int.MaxValue)] int page = 1)
    {
        var (tmdbNativeSeries, tmdbNativeCounts) = GetTmdbNativeDuplicateInfo();
        var enumerable = RepoFactory.MediaSeries.GetWithDuplicateFiles().Concat(tmdbNativeSeries);

        if (onlyFinishedSeries)
            enumerable = enumerable.Where(a =>
                a.AniDB_Anime?.GetFinishedAiring() ?? false
                || (a.TMDB_MovieID.HasValue && RepoFactory.TMDB_Movie.GetByTmdbMovieID(a.TMDB_MovieID.Value)?.ReleasedAt is { } r && r.ToDateTime(TimeOnly.MinValue) < System.DateTime.Today)
                || (a.TMDB_ShowID.HasValue && RepoFactory.TMDB_Show.GetByTmdbShowID(a.TMDB_ShowID.Value)?.LastAiredAt is { } last && last.ToDateTime(TimeOnly.MinValue) < System.DateTime.Today));

        return enumerable
            .OrderBy(series => series.Title)
            .ThenBy(series => series.AniDB_ID ?? 0)
            .ToListResult(series =>
            {
                var count = series.AniDB_ID is not (null or 0)
                    ? RepoFactory.MediaEpisode.GetWithDuplicateFiles(series.AniDB_ID).Count()
                    : tmdbNativeCounts.GetValueOrDefault(series.MediaSeriesID, 0);
                return new Series.WithEpisodeCount(count, series, User.JMMUserID, includeDataFrom);
            }, page, pageSize);
    }

    /// <summary>
    /// Get episodes with duplicate files for a series, with only the files with duplicates for each episode.
    /// </summary>
    /// <param name="seriesID">DaCollector Series ID</param>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSourceType"/>s.</param>
    /// <param name="includeMediaInfo">Include media info data.</param>
    /// <param name="includeXRefs">Include file/episode cross-references with the episodes.</param>
    /// <param name="includeReleaseInfo">Include release info data.</param>
    /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
    /// <param name="page">Page number.</param>
    /// <returns></returns>
    [HttpGet("Series/{seriesID}/Episodes")]
    public ActionResult<ListResult<Episode>> GetEpisodesForSeries(
        [FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSourceType> includeDataFrom = null,
        [FromQuery] bool includeMediaInfo = true,
        [FromQuery] bool includeXRefs = false,
        [FromQuery] bool includeReleaseInfo = false,
        [FromQuery, Range(0, 1000)] int pageSize = 100,
        [FromQuery, Range(1, int.MaxValue)] int page = 1)
    {
        var series = RepoFactory.MediaSeries.GetByID(seriesID);
        if (series == null)
            return new ListResult<Episode>();

        if (!User.AllowedSeries(series))
            return new ListResult<Episode>();

        if (series.AniDB_ID is null or 0)
            return GetTmdbNativeDuplicateEpisodesForSeries(series, includeDataFrom, includeXRefs, includeReleaseInfo, includeMediaInfo, page, pageSize);

        var enumerable = RepoFactory.MediaEpisode.GetWithDuplicateFiles(series.AniDB_ID)
            .Where(ep => ep.MediaSeriesID == series.MediaSeriesID);

        return enumerable
            .ToListResult(episode =>
            {
                var duplicateFiles = DuplicateFilesFor(episode.VideoLocals);
                var dto = new Episode(HttpContext, episode, includeDataFrom);
                dto.Size = duplicateFiles.Count;
                dto.Files = duplicateFiles.Select(f => new File(HttpContext, f, includeXRefs, includeReleaseInfo, includeMediaInfo, true)).ToList();
                return dto;
            }, page, pageSize);
    }

    /// <summary>
    /// Get the list of file location ids to auto remove for the series.
    /// </summary>
    /// <param name="seriesID">DaCollector Series ID</param>
    /// <returns></returns>
    [HttpGet("Series/{seriesID}/FileLocationsToAutoRemove")]
    public ActionResult<List<FileIdSet>> GetFileLocationsIdsAcrossAllEpisodes(
        [FromRoute, Range(1, int.MaxValue)] int seriesID
    )
    {
        var series = RepoFactory.MediaSeries.GetByID(seriesID);
        if (series == null)
            return new List<FileIdSet>();

        if (!User.AllowedSeries(series))
            return new List<FileIdSet>();

        if (series.AniDB_ID is null or 0)
        {
            var tmdbFiles = series.TMDB_MovieID.HasValue
                ? RepoFactory.CrossRef_File_TmdbMovie.GetByTmdbMovieID(series.TMDB_MovieID.Value)
                    .Select(x => RepoFactory.VideoLocal.GetByID(x.VideoLocalID)).WhereNotNull()
                : series.TMDB_ShowID.HasValue
                    ? RepoFactory.TMDB_Episode.GetByTmdbShowID(series.TMDB_ShowID.Value)
                        .SelectMany(ep => RepoFactory.CrossRef_File_TmdbEpisode.GetByTmdbEpisodeID(ep.TmdbEpisodeID))
                        .Select(x => RepoFactory.VideoLocal.GetByID(x.VideoLocalID)).WhereNotNull()
                    : [];

            return tmdbFiles
                .SelectMany(f => f.Places
                    .ExceptBy((f.FirstValidPlace ?? f.FirstResolvedPlace) is { } p ? [p.ID] : [], b => b.ID)
                    .Select(place => (series.MediaSeriesID, MediaEpisodeID: 0, place.VideoID, place.ID)))
                .GroupBy(t => t.VideoID, t => (t.ID, t.MediaEpisodeID, t.MediaSeriesID))
                .Select(g => new FileIdSet(g))
                .ToList();
        }

        var enumerable = RepoFactory.MediaEpisode.GetWithDuplicateFiles(series.AniDB_ID ?? 0);

        return enumerable
            .SelectMany(episode =>
                episode.VideoLocals
                    .SelectMany(a => a.Places.ExceptBy((a.FirstValidPlace ?? a.FirstResolvedPlace) is { } fileLocation ? [fileLocation.ID] : [], b => b.ID))
                    .Select(file => (episode.MediaSeriesID, episode.MediaEpisodeID, file.VideoID, file.ID))
            )
            .GroupBy(tuple => tuple.VideoID, tuple => (tuple.ID, tuple.MediaEpisodeID, tuple.MediaSeriesID))
            .Select(groupBy => new FileIdSet(groupBy))
            .ToList();
    }

    // Returns the VideoLocal records that have more than one place (i.e. the file exists in multiple locations)
    private static List<VideoLocal> DuplicateFilesFor(IEnumerable<VideoLocal> files) =>
        files
            .Where(f => f.Places.ExceptBy(
                (f.FirstValidPlace ?? f.FirstResolvedPlace) is { } p ? [p.ID] : [], b => b.ID).Any())
            .ToList();

    // Returns TMDB-native series that have any files with >1 location, and the count of affected "episodes" per series
    private static (IReadOnlyList<MediaSeries> series, IReadOnlyDictionary<int, int> countBySeriesID) GetTmdbNativeDuplicateInfo()
    {
        var duplicateVids = RepoFactory.VideoLocal.GetAll()
            .Where(vl => !string.IsNullOrEmpty(vl.Hash) && vl.Places.Count > 1)
            .Select(vl => vl.VideoLocalID)
            .ToHashSet();

        if (duplicateVids.Count == 0)
            return ([], new Dictionary<int, int>());

        var allSeries = RepoFactory.MediaSeries.GetAll();
        var movieIdToSeries = allSeries.Where(s => s.TMDB_MovieID.HasValue)
            .ToDictionary(s => s.TMDB_MovieID!.Value, s => s);
        var showIdToSeries = allSeries.Where(s => s.TMDB_ShowID.HasValue)
            .ToDictionary(s => s.TMDB_ShowID!.Value, s => s);

        // seriesID → distinct set of TMDB "episode" IDs that have duplicate files
        var seriesKeys = new Dictionary<int, HashSet<int>>();

        foreach (var videoLocalID in duplicateVids)
        {
            foreach (var xref in RepoFactory.CrossRef_File_TmdbMovie.GetByVideoLocalID(videoLocalID))
            {
                if (!movieIdToSeries.TryGetValue(xref.TmdbMovieID, out var s)) continue;
                if (!seriesKeys.TryGetValue(s.MediaSeriesID, out var set)) seriesKeys[s.MediaSeriesID] = set = [];
                set.Add(xref.TmdbMovieID);
            }
            foreach (var xref in RepoFactory.CrossRef_File_TmdbEpisode.GetByVideoLocalID(videoLocalID))
            {
                var ep = RepoFactory.TMDB_Episode.GetByTmdbEpisodeID(xref.TmdbEpisodeID);
                if (ep is null) continue;
                if (!showIdToSeries.TryGetValue(ep.TmdbShowID, out var s)) continue;
                if (!seriesKeys.TryGetValue(s.MediaSeriesID, out var set)) seriesKeys[s.MediaSeriesID] = set = [];
                set.Add(xref.TmdbEpisodeID);
            }
        }

        var seriesList = seriesKeys.Keys
            .Select(id => RepoFactory.MediaSeries.GetByID(id))
            .WhereNotNull()
            .ToList();
        var counts = seriesKeys.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Count);
        return (seriesList, counts);
    }

    // Returns Episode DTOs for all TMDB-native "episodes" (movies or show episodes) that have duplicate file locations
    private IEnumerable<Episode> GetTmdbNativeDuplicateEpisodes(HashSet<DataSourceType> includeDataFrom, bool includeXRefs, bool includeReleaseInfo, bool includeMediaInfo)
    {
        var allSeries = RepoFactory.MediaSeries.GetAll().Where(s => s.AniDB_ID is null or 0);
        foreach (var series in allSeries)
        {
            if (series.TMDB_MovieID.HasValue)
            {
                var movie = RepoFactory.TMDB_Movie.GetByTmdbMovieID(series.TMDB_MovieID.Value);
                if (movie is null) continue;
                var dupFiles = DuplicateFilesFor(
                    RepoFactory.CrossRef_File_TmdbMovie.GetByTmdbMovieID(movie.TmdbMovieID)
                        .Select(x => RepoFactory.VideoLocal.GetByID(x.VideoLocalID)).WhereNotNull());
                if (dupFiles.Count == 0) continue;
                var dto = new Episode(HttpContext, movie, series, includeDataFrom);
                dto.Size = dupFiles.Count;
                dto.Files = dupFiles.Select(f => new File(HttpContext, f, includeXRefs, includeReleaseInfo, includeMediaInfo, true)).ToList();
                yield return dto;
            }
            else if (series.TMDB_ShowID.HasValue)
            {
                foreach (var ep in RepoFactory.TMDB_Episode.GetByTmdbShowID(series.TMDB_ShowID.Value))
                {
                    var dupFiles = DuplicateFilesFor(
                        RepoFactory.CrossRef_File_TmdbEpisode.GetByTmdbEpisodeID(ep.TmdbEpisodeID)
                            .Select(x => RepoFactory.VideoLocal.GetByID(x.VideoLocalID)).WhereNotNull());
                    if (dupFiles.Count == 0) continue;
                    var dto = new Episode(HttpContext, ep, series, includeDataFrom);
                    dto.Size = dupFiles.Count;
                    dto.Files = dupFiles.Select(f => new File(HttpContext, f, includeXRefs, includeReleaseInfo, includeMediaInfo, true)).ToList();
                    yield return dto;
                }
            }
        }
    }

    // Returns paginated Episode DTOs for a TMDB-native series with duplicate files
    private ListResult<Episode> GetTmdbNativeDuplicateEpisodesForSeries(
        MediaSeries series,
        HashSet<DataSourceType> includeDataFrom,
        bool includeXRefs, bool includeReleaseInfo, bool includeMediaInfo,
        int page, int pageSize)
    {
        if (series.TMDB_MovieID.HasValue)
        {
            var movie = RepoFactory.TMDB_Movie.GetByTmdbMovieID(series.TMDB_MovieID.Value);
            if (movie is null) return new ListResult<Episode>();
            var dupFiles = DuplicateFilesFor(
                RepoFactory.CrossRef_File_TmdbMovie.GetByTmdbMovieID(movie.TmdbMovieID)
                    .Select(x => RepoFactory.VideoLocal.GetByID(x.VideoLocalID)).WhereNotNull());
            if (dupFiles.Count == 0) return new ListResult<Episode>();
            var dto = new Episode(HttpContext, movie, series, includeDataFrom);
            dto.Size = dupFiles.Count;
            dto.Files = dupFiles.Select(f => new File(HttpContext, f, includeXRefs, includeReleaseInfo, includeMediaInfo, true)).ToList();
            return new ListResult<Episode>(1, [dto]);
        }

        if (series.TMDB_ShowID.HasValue)
        {
            return RepoFactory.TMDB_Episode.GetByTmdbShowID(series.TMDB_ShowID.Value)
                .Select(ep =>
                {
                    var dupFiles = DuplicateFilesFor(
                        RepoFactory.CrossRef_File_TmdbEpisode.GetByTmdbEpisodeID(ep.TmdbEpisodeID)
                            .Select(x => RepoFactory.VideoLocal.GetByID(x.VideoLocalID)).WhereNotNull());
                    return (ep, dupFiles);
                })
                .Where(t => t.dupFiles.Count > 0)
                .ToListResult(t =>
                {
                    var dto = new Episode(HttpContext, t.ep, series, includeDataFrom);
                    dto.Size = t.dupFiles.Count;
                    dto.Files = t.dupFiles.Select(f => new File(HttpContext, f, includeXRefs, includeReleaseInfo, includeMediaInfo, true)).ToList();
                    return dto;
                }, page, pageSize);
        }

        return new ListResult<Episode>();
    }

    public class FileIdSet(IGrouping<int, (int VideoLocal_Place_ID, int MediaEpisodeID, int MediaSeriesID)> grouping)
    {
        /// <summary>
        /// The file ID with duplicates to remove.
        /// </summary>
        public int FileID { get; set; } = grouping.Key;

        /// <summary>
        /// The series IDs with duplicates to remove.
        /// </summary>
        public List<int> AnimeSeriesIDs { get; set; } = grouping
            .Select(tuple => tuple.MediaSeriesID)
            .Distinct()
            .ToList();

        /// <summary>
        /// The episode IDs with duplicates to remove.
        /// </summary>
        public List<int> AnimeEpisodeIDs { get; set; } = grouping
            .Select(tuple => tuple.MediaEpisodeID)
            .Distinct()
            .ToList();

        /// <summary>
        /// The duplicate locations to remove from the files/episodes.
        /// </summary>
        public List<int> FileLocationIDs { get; set; } = grouping
            .Select(tuple => tuple.VideoLocal_Place_ID)
            .Distinct()
            .ToList();
    }
}
