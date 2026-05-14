using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentNHibernate.Utils;
using Microsoft.Extensions.Logging;
using Quartz;
using DaCollector.Abstractions.Metadata.Enums;
using DaCollector.Abstractions.Extensions;
using DaCollector.Abstractions.Plugin;
using DaCollector.Abstractions.Video.Services;
using DaCollector.Server.Databases;
using DaCollector.Server.Extensions;
using DaCollector.Server.Models.DaCollector;
using DaCollector.Server.Providers.TMDB;
using DaCollector.Server.Repositories;
using DaCollector.Server.Scheduling;
using DaCollector.Server.Scheduling.Jobs.Actions;
using DaCollector.Server.Scheduling.Jobs.DaCollector;
using DaCollector.Server.Scheduling.Jobs.TMDB;
using DaCollector.Server.Server;
using DaCollector.Server.Settings;

using Utils = DaCollector.Server.Utilities.Utils;

namespace DaCollector.Server.Services;

public class ActionService
{
    private readonly ILogger<ActionService> _logger;

    private readonly ISchedulerFactory _schedulerFactory;

    private readonly ISettingsProvider _settingsProvider;

    private readonly IVideoReleaseService _videoReleaseService;

    private readonly IVideoService _videoService;

    private readonly TmdbMetadataService _tmdbService;

    private readonly DatabaseFactory _databaseFactory;

    private readonly IPluginPackageManager _pluginPackageManager;

    private readonly BackgroundWorker _downloadImagesWorker;

    public ActionService(
        ILogger<ActionService> logger,
        ISchedulerFactory schedulerFactory,
        ISettingsProvider settingsProvider,
        IVideoReleaseService videoReleaseService,
        IVideoService videoService,
        TmdbMetadataService tmdbService,
        DatabaseFactory databaseFactory,
        IPluginPackageManager pluginPackageManager
    )
    {
        _logger = logger;
        _schedulerFactory = schedulerFactory;
        _settingsProvider = settingsProvider;
        _videoReleaseService = videoReleaseService;
        _videoService = videoService;
        _tmdbService = tmdbService;
        _databaseFactory = databaseFactory;
        _pluginPackageManager = pluginPackageManager;
        _downloadImagesWorker = new();
        _downloadImagesWorker.DoWork += DownloadImagesWorker_DoWork;
        _downloadImagesWorker.WorkerSupportsCancellation = true;
    }

    public async Task RunImport_IntegrityCheck()
    {
        var scheduler = await _schedulerFactory.GetScheduler();
        // files which have not been hashed yet
        // or files which do not have a VideoInfo record
        var filesToHash = RepoFactory.VideoLocal.GetVideosWithoutHash();
        var dictFilesToHash = new Dictionary<int, VideoLocal>();
        foreach (var vl in filesToHash)
        {
            dictFilesToHash[vl.VideoLocalID] = vl;
            var p = vl.FirstResolvedPlace;
            if (p == null) continue;

            await scheduler.StartJob<HashFileJob>(c => c.FilePath = p.Path);
        }

        foreach (var vl in filesToHash)
        {
            // don't use if it is in the previous list
            if (dictFilesToHash.ContainsKey(vl.VideoLocalID)) continue;

            try
            {
                var p = vl.FirstResolvedPlace;
                if (p == null) continue;

                await scheduler.StartJob<HashFileJob>(c => c.FilePath = p.Path);
            }
            catch (Exception ex)
            {
                _logger.LogInformation("Error RunImport_IntegrityCheck XREF: {Detailed} - {Ex}", vl.ToStringDetailed(), ex.ToString());
            }
        }

        if (!_videoReleaseService.AutoMatchEnabled)
            return;

        // files which have been hashed, but don't have an associated episode
        var settings = _settingsProvider.GetSettings();
        var filesWithoutEpisode = RepoFactory.VideoLocal.GetVideosWithoutEpisode();
        foreach (var vl in filesWithoutEpisode)
        {
            if (settings.Import.MaxAutoScanAttemptsPerFile != 0)
            {
                var matchAttempts = RepoFactory.StoredReleaseInfo_MatchAttempt.GetByEd2kAndFileSize(vl.Hash, vl.FileSize).Count;
                if (matchAttempts > settings.Import.MaxAutoScanAttemptsPerFile)
                    continue;
            }

            await _videoReleaseService.ScheduleFindReleaseForVideo(vl);
        }
    }

    public void RunImport_GetImages()
    {
        if (!_downloadImagesWorker.IsBusy)
            _downloadImagesWorker.RunWorkerAsync();
    }

    private void DownloadImagesWorker_DoWork(object sender, DoWorkEventArgs e)
        => RunImport_GetImagesInternal().ConfigureAwait(false).GetAwaiter().GetResult();

    private async Task RunImport_GetImagesInternal()
    {
        var settings = _settingsProvider.GetSettings();
        // TMDB Images
        if (settings.TMDB.AutoDownloadPosters)
            await RunImport_DownloadTmdbImagesForType(_schedulerFactory, ImageEntityType.Poster, settings.TMDB.MaxAutoPosters);
        if (settings.TMDB.AutoDownloadLogos)
            await RunImport_DownloadTmdbImagesForType(_schedulerFactory, ImageEntityType.Logo, settings.TMDB.MaxAutoLogos);
        if (settings.TMDB.AutoDownloadBackdrops)
            await RunImport_DownloadTmdbImagesForType(_schedulerFactory, ImageEntityType.Backdrop, settings.TMDB.MaxAutoBackdrops);
        if (settings.TMDB.AutoDownloadStaffImages)
            await RunImport_DownloadTmdbImagesForType(_schedulerFactory, ImageEntityType.Creator, settings.TMDB.MaxAutoStaffImages);
        if (settings.TMDB.AutoDownloadThumbnails)
            await RunImport_DownloadTmdbImagesForType(_schedulerFactory, ImageEntityType.Thumbnail, settings.TMDB.MaxAutoThumbnails);
    }

    private static async Task RunImport_DownloadTmdbImagesForType(ISchedulerFactory schedulerFactory, ImageEntityType type, int maxCount)
    {
        // Build a few dictionaries to check how many images exist for each type.
        var countsForMovies = new Dictionary<int, int>();
        var countForEpisodes = new Dictionary<int, int>();
        var countForSeasons = new Dictionary<int, int>();
        var countForShows = new Dictionary<int, int>();
        var countForCollections = new Dictionary<int, int>();
        var countForNetworks = new Dictionary<int, int>();
        var countForCompanies = new Dictionary<int, int>();
        var countForPersons = new Dictionary<int, int>();
        var allImages = RepoFactory.TMDB_Image.GetByType(type);
        foreach (var image in allImages)
        {
            var path = image.LocalPath;
            if (string.IsNullOrEmpty(path))
                continue;

            if (!File.Exists(path))
                continue;

            var entities = RepoFactory.TMDB_Image_Entity.GetByRemoteFileName(image.RemoteFileName)
                .Where(x => x.ImageType == type)
                .ToList();
            foreach (var entity in entities)
                switch (entity.TmdbEntityType)
                {
                    case ForeignEntityType.Movie:
                        if (countsForMovies.ContainsKey(entity.TmdbEntityID))
                            countsForMovies[entity.TmdbEntityID] += 1;
                        else
                            countsForMovies[entity.TmdbEntityID] = 1;
                        break;
                    case ForeignEntityType.Episode:
                        if (countForEpisodes.ContainsKey(entity.TmdbEntityID))
                            countForEpisodes[entity.TmdbEntityID] += 1;
                        else
                            countForEpisodes[entity.TmdbEntityID] = 1;
                        break;
                    case ForeignEntityType.Season:
                        if (countForSeasons.ContainsKey(entity.TmdbEntityID))
                            countForSeasons[entity.TmdbEntityID] += 1;
                        else
                            countForSeasons[entity.TmdbEntityID] = 1;
                        break;
                    case ForeignEntityType.Show:
                        if (countForShows.ContainsKey(entity.TmdbEntityID))
                            countForShows[entity.TmdbEntityID] += 1;
                        else
                            countForShows[entity.TmdbEntityID] = 1;
                        break;
                    case ForeignEntityType.Collection:
                        if (countForCollections.ContainsKey(entity.TmdbEntityID))
                            countForCollections[entity.TmdbEntityID] += 1;
                        else
                            countForCollections[entity.TmdbEntityID] = 1;
                        break;
                    case ForeignEntityType.Network:
                        if (countForNetworks.ContainsKey(entity.TmdbEntityID))
                            countForNetworks[entity.TmdbEntityID] += 1;
                        else
                            countForNetworks[entity.TmdbEntityID] = 1;
                        break;
                    case ForeignEntityType.Company:
                        if (countForCompanies.ContainsKey(entity.TmdbEntityID))
                            countForCompanies[entity.TmdbEntityID] += 1;
                        else
                            countForCompanies[entity.TmdbEntityID] = 1;
                        break;
                    case ForeignEntityType.Person:
                        if (countForPersons.ContainsKey(entity.TmdbEntityID))
                            countForPersons[entity.TmdbEntityID] += 1;
                        else
                            countForPersons[entity.TmdbEntityID] = 1;
                        break;
                }
        }

        var scheduler = await schedulerFactory.GetScheduler();
        foreach (var image in allImages)
        {
            var path = image.LocalPath;
            if (string.IsNullOrEmpty(path) || File.Exists(path))
                continue;

            // Check if we should download the image or not.
            var limitEnabled = maxCount > 0;
            var entities = RepoFactory.TMDB_Image_Entity.GetByRemoteFileName(image.RemoteFileName)
                .Where(x => x.ImageType == type)
                .ToList();
            var shouldDownload = !limitEnabled && entities.Count > 0;
            if (limitEnabled && entities.Count > 0)
                foreach (var entity in entities)
                    switch (entity.TmdbEntityType)
                    {
                        case ForeignEntityType.Movie:
                            if (countsForMovies.ContainsKey(entity.TmdbEntityID) && countsForMovies[entity.TmdbEntityID] < maxCount)
                                shouldDownload = true;
                            break;
                        case ForeignEntityType.Episode:
                            if (countForEpisodes.ContainsKey(entity.TmdbEntityID) && countForEpisodes[entity.TmdbEntityID] < maxCount)
                                shouldDownload = true;
                            break;
                        case ForeignEntityType.Season:
                            if (countForSeasons.ContainsKey(entity.TmdbEntityID) && countForSeasons[entity.TmdbEntityID] < maxCount)
                                shouldDownload = true;
                            break;
                        case ForeignEntityType.Show:
                            if (countForShows.ContainsKey(entity.TmdbEntityID) && countForShows[entity.TmdbEntityID] < maxCount)
                                shouldDownload = true;
                            break;
                        case ForeignEntityType.Collection:
                            if (countForCollections.ContainsKey(entity.TmdbEntityID) && countForCollections[entity.TmdbEntityID] < maxCount)
                                shouldDownload = true;
                            break;
                        case ForeignEntityType.Network:
                            if (countForNetworks.ContainsKey(entity.TmdbEntityID) && countForNetworks[entity.TmdbEntityID] < maxCount)
                                shouldDownload = true;
                            break;
                        case ForeignEntityType.Company:
                            if (countForCompanies.ContainsKey(entity.TmdbEntityID) && countForCompanies[entity.TmdbEntityID] < maxCount)
                                shouldDownload = true;
                            break;
                        case ForeignEntityType.Person:
                            if (countForPersons.ContainsKey(entity.TmdbEntityID) && countForPersons[entity.TmdbEntityID] < maxCount)
                                shouldDownload = true;
                            break;
                    }

            if (shouldDownload)
            {
                await scheduler.StartJob<DownloadTmdbImageJob>(c =>
                {
                    c.ImageID = image.TMDB_ImageID;
                    c.ImageType = image.ImageType;
                });

                foreach (var entity in entities)
                    switch (entity.TmdbEntityType)
                    {
                        case ForeignEntityType.Movie:
                            if (countsForMovies.ContainsKey(entity.TmdbEntityID))
                                countsForMovies[entity.TmdbEntityID] += 1;
                            else
                                countsForMovies[entity.TmdbEntityID] = 1;
                            break;
                        case ForeignEntityType.Episode:
                            if (countForEpisodes.ContainsKey(entity.TmdbEntityID))
                                countForEpisodes[entity.TmdbEntityID] += 1;
                            else
                                countForEpisodes[entity.TmdbEntityID] = 1;
                            break;
                        case ForeignEntityType.Season:
                            if (countForSeasons.ContainsKey(entity.TmdbEntityID))
                                countForSeasons[entity.TmdbEntityID] += 1;
                            else
                                countForSeasons[entity.TmdbEntityID] = 1;
                            break;
                        case ForeignEntityType.Show:
                            if (countForShows.ContainsKey(entity.TmdbEntityID))
                                countForShows[entity.TmdbEntityID] += 1;
                            else
                                countForShows[entity.TmdbEntityID] = 1;
                            break;
                        case ForeignEntityType.Collection:
                            if (countForCollections.ContainsKey(entity.TmdbEntityID))
                                countForCollections[entity.TmdbEntityID] += 1;
                            else
                                countForCollections[entity.TmdbEntityID] = 1;
                            break;
                        case ForeignEntityType.Network:
                            if (countForNetworks.ContainsKey(entity.TmdbEntityID))
                                countForNetworks[entity.TmdbEntityID] += 1;
                            else
                                countForNetworks[entity.TmdbEntityID] = 1;
                            break;
                        case ForeignEntityType.Company:
                            if (countForCompanies.ContainsKey(entity.TmdbEntityID))
                                countForCompanies[entity.TmdbEntityID] += 1;
                            else
                                countForCompanies[entity.TmdbEntityID] = 1;
                            break;
                        case ForeignEntityType.Person:
                            if (countForPersons.ContainsKey(entity.TmdbEntityID))
                                countForPersons[entity.TmdbEntityID] += 1;
                            else
                                countForPersons[entity.TmdbEntityID] = 1;
                            break;
                    }
            }
        }
    }

    public Task RunImport_ScanTMDB()
        => _tmdbService.ScanForMatches();

    public Task RunImport_PurgeUnlinkedTmdbPeople()
        => _tmdbService.PurgeUnlinkedPeople();

    public Task RunImport_PurgeUnlinkedTmdbShowNetworks()
        => _tmdbService.PurgeUnlinkedShowNetworks();

    public async Task RemoveRecordsWithoutPhysicalFiles(bool removeMyList = true)
    {
        var scheduler = await _schedulerFactory.GetScheduler();
        _logger.LogInformation("Remove Missing Files: Start");
        var seriesToUpdate = new HashSet<MediaSeries>();
        using var session = _databaseFactory.SessionFactory.OpenSession();

        // remove missing files in valid managed folders
        var filesAll = RepoFactory.VideoLocalPlace.GetAll()
            .Where(a => a.ManagedFolder != null)
            .GroupBy(a => a.ManagedFolder)
            .ToDictionary(a => a.Key, a => a.ToList());
        foreach (var vl in filesAll.Keys.SelectMany(a => filesAll[a]))
        {
            if (File.Exists(vl.Path)) continue;

            // delete video local record
            _logger.LogInformation("Removing Missing File: {ID}", vl.VideoID);
            await ((VideoService)_videoService).RemoveRecordWithOpenTransaction(session, vl, seriesToUpdate, removeMyList);
        }

        var videoLocalsAll = RepoFactory.VideoLocal.GetAll().ToList();
        // remove empty video locals
        BaseRepository.Lock(session, videoLocalsAll, (s, vls) =>
        {
            using var transaction = s.BeginTransaction();
            RepoFactory.VideoLocal.DeleteWithOpenTransaction(s, vls.Where(a => a.IsEmpty()).ToList());
            transaction.Commit();
        });

        // Remove duplicate video locals
        var locals = videoLocalsAll
            .Where(a => !string.IsNullOrWhiteSpace(a.Hash))
            .GroupBy(a => a.Hash)
            .ToDictionary(g => g.Key, g => g.ToList());
        var toRemove = new List<VideoLocal>();
        var comparer = new VideoLocalComparer();

        foreach (var hash in locals.Keys)
        {
            var values = locals[hash].ToList();
            values.Sort(comparer);
            var to = values.First();
            values.Remove(to);
            foreach (var places in values.Select(from => from.Places).Where(places => places != null && places.Count != 0))
            {
                BaseRepository.Lock(session, places, (s, ps) =>
                {
                    using var transaction = s.BeginTransaction();
                    foreach (var place in ps)
                    {
                        place.VideoID = to.VideoLocalID;
                        RepoFactory.VideoLocalPlace.SaveWithOpenTransaction(s, place);
                    }

                    transaction.Commit();
                });
            }

            toRemove.AddRange(values);
        }

        BaseRepository.Lock(session, toRemove, (s, ps) =>
        {
            using var transaction = s.BeginTransaction();
            foreach (var remove in ps)
            {
                RepoFactory.VideoLocal.DeleteWithOpenTransaction(s, remove);
            }

            transaction.Commit();
        });

        // Remove files in invalid managed folders
        foreach (var v in videoLocalsAll)
        {
            var places = v.Places;
            if (v.Places?.Count > 0)
            {
                BaseRepository.Lock(session, places, (s, ps) =>
                {
                    using var transaction = s.BeginTransaction();
                    foreach (var place in ps.Where(place => string.IsNullOrWhiteSpace(place?.Path)))
                    {
#pragma warning disable CS0618
                        _logger.LogInformation("Remove Records With Orphaned Managed Folder: {Filename}", v.FileName);
#pragma warning restore CS0618
                        seriesToUpdate.UnionWith(v.AnimeEpisodes.Select(a => a.MediaSeries)
                            .DistinctBy(a => a.MediaSeriesID));
                        RepoFactory.VideoLocalPlace.DeleteWithOpenTransaction(s, place);
                    }

                    transaction.Commit();
                });
            }

            // Remove duplicate places
            places = v.Places;
            if (places?.Count == 1) continue;

            if (places?.Count > 0)
            {
                places = places.DistinctBy(a => a.Path).ToList();
                places = v.Places?.Except(places).ToList() ?? [];
                foreach (var place in places)
                {
                    BaseRepository.Lock(session, place, (s, p) =>
                    {
                        using var transaction = s.BeginTransaction();
                        RepoFactory.VideoLocalPlace.DeleteWithOpenTransaction(s, p);
                        transaction.Commit();
                    });
                }
            }

            if (v.Places?.Count > 0) continue;

            // delete video local record
#pragma warning disable CS0618
            _logger.LogInformation("RemoveOrphanedVideoLocal : {Filename}", v.FileName);
#pragma warning restore CS0618
            seriesToUpdate.UnionWith(v.AnimeEpisodes.Select(a => a.MediaSeries)
                .DistinctBy(a => a.MediaSeriesID));

            BaseRepository.Lock(session, v, (s, vl) =>
            {
                using var transaction = s.BeginTransaction();
                RepoFactory.VideoLocal.DeleteWithOpenTransaction(s, vl);
                transaction.Commit();
            });
        }

        // Clean up failed imports
        var list = RepoFactory.VideoLocal.GetAll()
            .SelectMany(a => a.EpisodeCrossReferences)
            .Where(a => a.MetadataAnime == null || a.MetadataEpisode == null)
            .ToArray();
        BaseRepository.Lock(session, s =>
        {
            using var transaction = s.BeginTransaction();
            foreach (var xref in list)
            {
                // We don't need to update anything since they don't exist
                RepoFactory.CrossRef_File_Episode.DeleteWithOpenTransaction(s, xref);
            }

            transaction.Commit();
        });

        // clean up orphaned video local places
        var placesToRemove = RepoFactory.VideoLocalPlace.GetAll().Where(a => a.VideoLocal == null).ToList();
        BaseRepository.Lock(session, s =>
        {
            using var transaction = s.BeginTransaction();
            foreach (var place in placesToRemove)
            {
                // We don't need to update anything since they don't exist
                RepoFactory.VideoLocalPlace.DeleteWithOpenTransaction(s, place);
            }

            transaction.Commit();
        });

        // NOTE: use 'purge unused releases' if you want to remove the cross-references too.

        // update everything we modified
        await Task.WhenAll(seriesToUpdate.Select(a => scheduler.StartJob<RefreshAnimeStatsJob>(b => b.AnimeID = a.AniDB_ID ?? 0)));

        _logger.LogInformation("Remove Missing Files: Finished");
    }

    public async Task UpdateAllStats()
    {
        var scheduler = await _schedulerFactory.GetScheduler().ConfigureAwait(false);
        await Task.WhenAll(RepoFactory.MediaSeries.GetAll().Select(a => scheduler.StartJob<RefreshAnimeStatsJob>(b => b.AnimeID = a.AniDB_ID ?? 0)));
    }

    public void CheckForPreviouslyIgnored()
    {
        try
        {
            var filesAll = RepoFactory.VideoLocal.GetAll();
            IReadOnlyList<VideoLocal> filesIgnored = RepoFactory.VideoLocal.GetIgnoredVideos();

            foreach (var vl in filesAll)
            {
                if (!vl.IsIgnored)
                {
                    // Check if we have this file marked as previously ignored, matches only if it has the same hash
                    var resultVideoLocalsIgnored =
                        filesIgnored.Where(s => s.Hash == vl.Hash).ToList();

                    if (resultVideoLocalsIgnored.Count != 0)
                    {
                        vl.IsIgnored = true;
                        RepoFactory.VideoLocal.Save(vl, false);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in CheckForPreviouslyIgnored: {Ex}", ex);
        }
    }

    /// <summary>
    ///   Schedules a plugin update check job.
    /// </summary>
    /// <param name="forceRefresh">
    ///   Force sync even if not stale.
    /// </param>
    public async Task CheckForPluginUpdates(bool forceRefresh)
    {
        var settings = _settingsProvider.GetSettings();

        // Check if auto-sync is enabled (must be enabled unless forcing)
        if (!settings.Plugins.Updates.IsAutoSyncEnabled && !forceRefresh)
            return;

        // Check frequency setting (skip schedule check if forcing)
        if (!forceRefresh)
        {
            if (settings.Plugins.Updates.AutoUpdateFrequency is ScheduledUpdateFrequency.Never)
                return;

            var schedule = RepoFactory.ScheduledUpdate.GetByUpdateType((int)ScheduledUpdateType.PluginUpdates);
            if (schedule != null)
            {
                var freqHours = Utils.GetScheduledHours(settings.Plugins.Updates.AutoUpdateFrequency);
                var tsLastRun = DateTime.Now - schedule.LastUpdate;
                if (tsLastRun.TotalHours < freqHours)
                    return;
            }
        }

        await _pluginPackageManager.ScheduleCheckForUpdates(forceSync: forceRefresh ? true : null).ConfigureAwait(false);
    }
}
