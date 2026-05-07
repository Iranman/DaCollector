using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DaCollector.Server.API.Annotations;
using DaCollector.Server.API.ModelBinders;
using DaCollector.Server.API.v3.Helpers;
using DaCollector.Server.API.v3.Models.Common;
using DaCollector.Server.API.v3.Models.DaCollector;
using DaCollector.Server.Extensions;
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
        var enumerable = RepoFactory.MediaEpisode.GetWithDuplicateFiles();

        return enumerable
            .ToListResult(episode =>
            {
                var duplicateFiles = episode.VideoLocals
                    .Select(file => (file, locations: file.Places.ExceptBy((file.FirstValidPlace ?? file.FirstResolvedPlace) is { } fileLocation ? [fileLocation.ID] : [], b => b.ID).ToList()))
                    .Where(tuple => tuple.locations.Count > 0)
                    .ToList();
                var dto = new Episode(HttpContext, episode, includeDataFrom);
                dto.Size = duplicateFiles.Count;
                dto.Files = duplicateFiles
                    .Select(tuple => new File(HttpContext, tuple.file, includeXRefs, includeReleaseInfo, includeMediaInfo, true))
                    .ToList();
                return dto;
            }, page, pageSize);
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
        var enumerable = RepoFactory.MediaSeries.GetWithDuplicateFiles();
        if (onlyFinishedSeries)
            enumerable = enumerable.Where(a => a.AniDB_Anime.GetFinishedAiring());

        return enumerable
            .OrderBy(series => series.Title)
            .ThenBy(series => series.AniDB_ID)
            .ToListResult(series => new Series.WithEpisodeCount(RepoFactory.MediaEpisode.GetWithDuplicateFiles(series.AniDB_ID).Count(), series, User.JMMUserID, includeDataFrom), page, pageSize);
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

        var enumerable = RepoFactory.MediaEpisode.GetWithDuplicateFiles(series.AniDB_ID);

        return enumerable
            .ToListResult(episode =>
            {
                var duplicateFiles = episode.VideoLocals
                    .Select(file => (file, locations: file.Places.ExceptBy((file.FirstValidPlace ?? file.FirstResolvedPlace) is { } fileLocation ? [fileLocation.ID] : [], b => b.ID).ToList()))
                    .Where(tuple => tuple.locations.Count > 0)
                    .ToList();
                var dto = new Episode(HttpContext, episode, includeDataFrom);
                dto.Size = duplicateFiles.Count;
                dto.Files = duplicateFiles
                    .Select(tuple => new File(HttpContext, tuple.file, includeXRefs, includeReleaseInfo, includeMediaInfo, true))
                    .ToList();
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

        var enumerable = RepoFactory.MediaEpisode.GetWithDuplicateFiles(series.AniDB_ID);

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
