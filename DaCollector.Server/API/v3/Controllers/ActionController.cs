using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quartz;
using DaCollector.Abstractions.Video.Services;
using DaCollector.Server.API.Annotations;
using DaCollector.Server.API.ModelBinders;
using DaCollector.Server.API.v3.Models.DaCollector;
using DaCollector.Server.Providers.TMDB;
using DaCollector.Server.Repositories;
using DaCollector.Server.Scheduling;
using DaCollector.Server.Scheduling.Jobs.Actions;
using DaCollector.Server.Scheduling.Jobs.Plex;
using DaCollector.Server.Scheduling.Jobs.DaCollector;
using DaCollector.Server.Scheduling.Jobs.Trakt;
using DaCollector.Server.Services;
using DaCollector.Server.Settings;
using DaCollector.Server.Tasks;

#nullable enable
namespace DaCollector.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize]
public class ActionController : BaseController
{
    private readonly MediaGroupCreator _groupCreator;
    private readonly ActionService _actionService;
    private readonly MediaGroupService _groupService;
    private readonly TmdbMetadataService _tmdbMetadataService;
    private readonly TmdbLinkingService _tmdbLinkingService;
    private readonly TmdbImageService _tmdbImageService;
    private readonly IVideoService _videoService;
    private readonly IVideoReleaseService _videoReleaseService;
    private readonly ISchedulerFactory _schedulerFactory;

    public ActionController(
        TmdbMetadataService tmdbMetadataService,
        TmdbLinkingService tmdbLinkingService,
        TmdbImageService tmdbImageService,
        ISchedulerFactory schedulerFactory,
        IVideoService videoService,
        IVideoReleaseService videoReleaseService,
        ISettingsProvider settingsProvider,
        ActionService actionService,
        MediaGroupCreator groupCreator,
        MediaGroupService groupService
    ) : base(settingsProvider)
    {
        _tmdbMetadataService = tmdbMetadataService;
        _tmdbLinkingService = tmdbLinkingService;
        _tmdbImageService = tmdbImageService;
        _videoService = videoService;
        _videoReleaseService = videoReleaseService;
        _schedulerFactory = schedulerFactory;
        _actionService = actionService;
        _groupCreator = groupCreator;
        _groupService = groupService;
    }

    #region Common Actions

    /// <summary>
    /// Run Import. This checks for new files, hashes them etc, scans Drop Folders, checks and scans for community site links (tmdb, trakt, etc), and downloads missing images.
    /// </summary>
    /// <returns></returns>
    [HttpGet("RunImport")]
    public async Task<ActionResult> RunImport()
    {
        var scheduler = await _schedulerFactory.GetScheduler();
        await scheduler.StartJob<ImportJob>();
        return Ok();
    }

    /// <summary>
    /// Queues a task to import only new files found in the managed folders
    /// </summary>
    /// <returns></returns>
    [HttpGet("ImportNewFiles")]
    public async Task<ActionResult> ImportNewFiles()
    {
        await _videoService.ScheduleScanForManagedFolders(onlyNewFiles: true);
        return Ok();
    }

    /// <summary>
    /// This was for web cache hash syncing, and will be for perceptual hashing maybe eventually.
    /// </summary>
    /// <returns></returns>
    [HttpGet("SyncHashes")]
    public ActionResult SyncHashes()
    {
        return BadRequest();
    }

    /// <summary>
    /// Send local watch states to Trakt for the whole collection
    /// </summary>
    /// <returns></returns>
    [HttpGet("SendWatchStatesToTrakt")]
    public async Task<ActionResult> SendWatchStatesToTrakt()
    {
        var settings = SettingsProvider.GetSettings().TraktTv;
        if (!settings.Enabled || string.IsNullOrEmpty(settings.AuthToken))
        {
            return BadRequest("Trakt account is not linked!");
        }

        var scheduler = await _schedulerFactory.GetScheduler();
        await scheduler.StartJob<SendWatchStatesToTraktJob>(c => c.ForceRefresh = true, prioritize: true);

        return Ok();
    }

    /// <summary>
    /// Get remote watch states from Trakt for the whole collection
    /// </summary>
    /// <returns></returns>
    [HttpGet("GetWatchStatesFromTrakt")]
    public async Task<ActionResult> GetWatchStatesFromTrakt()
    {
        var settings = SettingsProvider.GetSettings().TraktTv;
        if (!settings.Enabled || string.IsNullOrEmpty(settings.AuthToken))
        {
            return BadRequest("Trakt account is not linked!");
        }

        var scheduler = await _schedulerFactory.GetScheduler();
        await scheduler.StartJob<GetWatchStatesFromTraktJob>(c => c.ForceRefresh = true, prioritize: true);

        return Ok();
    }

    /// <summary>
    /// Remove Entries in the DaCollector Database for Files that are no longer accessible
    /// </summary>
    /// <returns></returns>
    [HttpGet("RemoveMissingFiles/{removeFromMyList:bool?}")]
    public async Task<ActionResult> RemoveMissingFiles(bool removeFromMyList = true)
    {
        await _actionService.RemoveRecordsWithoutPhysicalFiles(removeFromMyList);
        return Ok();
    }

    /// <summary>
    /// Updates and Downloads Missing Images
    /// </summary>
    /// <returns></returns>
    [HttpGet("UpdateAllImages")]
    public ActionResult UpdateAllImages()
    {
        _actionService.RunImport_GetImages();
        return Ok();
    }

    /// <summary>
    /// Scan for TMDB matches for all unlinked movies and TV shows.
    /// </summary>
    /// <returns></returns>
    [HttpGet("SearchForTmdbMatches")]
    public ActionResult SearchForTmdbMatches()
    {
        Task.Factory.StartNew(() => _tmdbMetadataService.ScanForMatches());
        return Ok();
    }

    /// <summary>
    /// Updates all TMDB Movies in the local database.
    /// </summary>
    /// <returns></returns>
    [HttpGet("UpdateAllTmdbMovies")]
    public ActionResult UpdateAllTmdbMovies()
    {
        Task.Factory.StartNew(() => _tmdbMetadataService.UpdateAllMovies(true, true));
        return Ok();
    }

    /// <summary>
    /// Purge all unused TMDB Movies that are not linked to local media.
    /// </summary>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpGet("PurgeAllUnusedTmdbMovies")]
    public ActionResult PurgeAllUnusedTmdbMovies()
    {
        Task.Factory.StartNew(() => _tmdbMetadataService.PurgeAllUnusedMovies());
        return Ok();
    }

    /// <summary>
    /// Purge all TMDB Movie Collections.
    /// </summary>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpGet("PurgeAllTmdbMovieCollections")]
    public ActionResult PurgeAllTmdbMovieCollections()
    {
        Task.Factory.StartNew(() => _tmdbMetadataService.PurgeAllMovieCollections());
        return Ok();
    }

    /// <summary>
    /// Update all TMDB Shows in the local database.
    /// </summary>
    /// <returns></returns>
    [HttpGet("UpdateAllTmdbShows")]
    public ActionResult UpdateAllTmdbShows()
    {
        Task.Factory.StartNew(() => _tmdbMetadataService.UpdateAllShows(true, true));
        return Ok();
    }

    /// <summary>
    /// Download any missing TMDB People.
    /// </summary>
    [HttpGet("DownloadMissingTmdbPeople")]
    public ActionResult DownloadMissingTmdbPeople()
    {
        Task.Factory.StartNew(() => _tmdbMetadataService.RepairMissingPeople());
        return Ok();
    }

    /// <summary>
    /// Purge all unused TMDB Images that are not linked to anything.
    /// </summary>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpGet("PurgeAllUnusedTmdbImages")]
    public ActionResult PurgeAllUnusedTmdbImages()
    {
        Task.Factory.StartNew(_tmdbImageService.PurgeAllUnusedImages);
        return Ok();
    }

    /// <summary>
    /// Purge all unused TMDB Shows that are not linked to local media.
    /// </summary>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpGet("PurgeAllUnusedTmdbShows")]
    public ActionResult PurgeAllUnusedTmdbShows()
    {
        Task.Factory.StartNew(() => _tmdbMetadataService.PurgeAllUnusedShows());
        return Ok();
    }

    /// <summary>
    /// Purge all TMDB Show Alternate Orderings.
    /// </summary>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpGet("PurgeAllTmdbShowAlternateOrderings")]
    public ActionResult PurgeAllTmdbShowAlternateOrderings()
    {
        Task.Factory.StartNew(_tmdbMetadataService.PurgeAllShowEpisodeGroups);
        return Ok();
    }

    /// <summary>
    /// Purge all TMDB links, optionally removing the links and resetting the auto-linking state.
    /// </summary>
    /// <param name="removeShowLinks">Whether to remove show links.</param>
    /// <param name="removeMovieLinks">Whether to remove movie links.</param>
    /// <param name="resetAutoLinkingState">Whether to reset the auto-linking state.</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpGet("PurgeAllTmdbLinks")]
    public ActionResult PurgeAllTmdbLinks([FromQuery] bool removeShowLinks = true, [FromQuery] bool removeMovieLinks = true, [FromQuery] bool? resetAutoLinkingState = null)
    {
        Task.Run(() =>
        {
            if (removeShowLinks || removeMovieLinks)
                _tmdbLinkingService.RemoveAllLinks(removeShowLinks, removeMovieLinks);
            if (resetAutoLinkingState.HasValue)
                _tmdbLinkingService.ResetAutoLinkingState(resetAutoLinkingState.Value);
        });
        return Ok();
    }

    /// <summary>
    /// Clears the current release for all known videos.
    /// </summary>
    /// <param name="removeFromMylist">
    ///   Set to <c>false</c> to not remove the release from the user's MyList.
    /// </param>
    /// <param name="providerNames">
    ///   The names of the providers to clear. If null, all providers will be cleared.
    /// </param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpGet("PurgeAllUsedReleases")]
    public ActionResult PurgeAllUsedReleases(
        [FromQuery] bool removeFromMylist = true,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<string>? providerNames = null
    )
    {
        Task.Run(() => _videoReleaseService.PurgeUsedReleases(providerNames, removeFromMylist));
        return Ok();
    }

    /// <summary>
    /// Purges all unused releases not linked to any videos from the database.
    /// </summary>
    /// <param name="removeFromMylist">
    ///   Set to <c>false</c> to not remove the release from the user's MyList.
    /// </param>
    /// <param name="providerNames">
    ///   The names of the providers to clear. If null, all providers will be cleared.
    /// </param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpGet("PurgeAllUnusedReleases")]
    public ActionResult PurgeAllUnusedReleases(
        [FromQuery] bool removeFromMylist = true,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<string>? providerNames = null
    )
    {
        Task.Run(() => _videoReleaseService.PurgeUnusedReleases(providerNames, removeFromMylist));
        return Ok();
    }

    /// <summary>
    /// Validates invalid images and re-downloads them
    /// </summary>
    /// <returns></returns>
    [HttpGet("ValidateAllImages")]
    public async Task<ActionResult> ValidateAllImages()
    {
        var scheduler = await _schedulerFactory.GetScheduler();
        await scheduler.StartJob<ValidateAllImagesJob>(prioritize: true);
        return Ok();
    }

    #endregion

    #region Admin Actions

    /// <summary>
    /// Queues a task to Update all media info
    /// </summary>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpGet("UpdateAllMediaInfo")]
    public async Task<ActionResult> UpdateAllMediaInfo()
    {
        var scheduler = await _schedulerFactory.GetScheduler();
        await scheduler.StartJob<MediaInfoAllFilesJob>();
        return Ok();
    }

    /// <summary>
    /// Queues commands to Update All Series Stats and Force a Recalculation of All Group Filters
    /// </summary>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpGet("UpdateSeriesStats")]
    public async Task<ActionResult> UpdateSeriesStats()
    {
        await _actionService.UpdateAllStats();
        return Ok();
    }

    /// <summary>
    /// Recreate all <see cref="Group"/>s. This will delete any and all existing groups.
    /// </summary>
    /// <remarks>
    /// This action requires an admin account because it's a destructive action.
    /// </remarks>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpGet("RecreateAllGroups")]
    public ActionResult RecreateAllGroups()
    {
        Task.Factory.StartNew(() => _groupCreator.RecreateAllGroups()).ConfigureAwait(false);
        return Ok();
    }

    /// <summary>
    /// Rename al <see cref="Group"/>s. This won't recreate the whole library,
    /// only rename any groups without a custom name set based on the current
    /// language preference.
    /// </summary>
    /// <remarks>
    /// This action requires an admin account because it affects all groups.
    /// </remarks>
    [Authorize("admin")]
    [HttpGet("RenameAllGroups")]
    public ActionResult RenameAllGroups()
    {
        Task.Factory.StartNew(_groupService.RenameAllGroups, TaskCreationOptions.LongRunning).ConfigureAwait(false);
        return Ok();
    }

    /// <summary>
    /// Add all series that have data and files, but no series. This helps if you've deleted a series, and it's stuck in limbo.
    /// </summary>
    /// <remarks>
    /// This action requires an admin account because it affects the collection.
    /// </remarks>
    [Authorize("admin")]
    [HttpGet("CreateMissingSeries")]
    public async Task<ActionResult> CreateMissingSeries()
    {
        await _actionService.CreateMissingSeries();
        return Ok();
    }

    /// <summary>
    /// Sync watch states with plex.
    /// </summary>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpGet("PlexSyncAll")]
    public async Task<ActionResult> PlexSyncAll()
    {
        var scheduler = await _schedulerFactory.GetScheduler();
        foreach (var user in RepoFactory.JMMUser.GetAll())
        {
            if (string.IsNullOrEmpty(user.PlexToken)) continue;
            await scheduler.StartJob<SyncPlexWatchedStatesJob>(c => c.User = user);
        }
        return Ok();
    }

    #endregion
}
