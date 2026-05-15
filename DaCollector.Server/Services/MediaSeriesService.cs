using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Force.DeepCloner;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;
using DaCollector.Abstractions.Metadata;
using DaCollector.Abstractions.Metadata.Enums;
using DaCollector.Abstractions.Extensions;
using DaCollector.Abstractions.User.Services;
using DaCollector.Abstractions.Video.Services;
using DaCollector.Server.Extensions;
using DaCollector.Server.Models.AniDB;
using DaCollector.Server.Models.DaCollector;
using DaCollector.Server.Repositories;
using DaCollector.Server.Repositories.Cached;
using DaCollector.Server.Scheduling;
using DaCollector.Server.Scheduling.Jobs.Actions;
using DaCollector.Server.Tasks;
using DaCollector.Server.Utilities;

using MediaType = DaCollector.Abstractions.Metadata.Enums.MediaType;
using EpisodeType = DaCollector.Abstractions.Metadata.Enums.EpisodeType;

#nullable enable
namespace DaCollector.Server.Services;

public class MediaSeriesService
{
    private readonly ILogger<MediaSeriesService> _logger;
    private readonly VideoLocal_UserRepository _vlUsers;
    private readonly MediaGroupService _groupService;
    private readonly MediaGroupCreator _groupCreator;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly IVideoReleaseService _videoReleaseService;
    private readonly UserDataService _userDataService;

    public MediaSeriesService(ILogger<MediaSeriesService> logger, ISchedulerFactory schedulerFactory, MediaGroupService groupService, MediaGroupCreator groupCreator, VideoLocal_UserRepository vlUsers, IVideoReleaseService videoReleaseService, IUserDataService userDataService)
    {
        _logger = logger;
        _schedulerFactory = schedulerFactory;
        _groupService = groupService;
        _groupCreator = groupCreator;
        _vlUsers = vlUsers;
        _videoReleaseService = videoReleaseService;
        _userDataService = (UserDataService)userDataService;
    }

    public MediaSeries GetOrCreateSeriesFromProvider(string provider, int providerID, string mediaType)
    {
        var existing = provider switch
        {
            "tmdb" when mediaType == "show" => RepoFactory.MediaSeries.GetAll().FirstOrDefault(s => s.TMDB_ShowID == providerID),
            "tmdb" when mediaType == "movie" => RepoFactory.MediaSeries.GetAll().FirstOrDefault(s => s.TMDB_MovieID == providerID),
            "tvdb" when mediaType == "show" => RepoFactory.MediaSeries.GetAll().FirstOrDefault(s => s.TvdbShowExternalID == providerID),
            "tvdb" when mediaType == "movie" => RepoFactory.MediaSeries.GetAll().FirstOrDefault(s => s.TvdbMovieExternalID == providerID),
            _ => null
        };

        if (existing is not null)
            return existing;

        var now = DateTime.Now;
        var series = new MediaSeries
        {
            AniDB_ID = null,
            LatestLocalEpisodeNumber = 0,
            DateTimeUpdated = now,
            DateTimeCreated = now,
            UpdatedAt = now,
            SeriesNameOverride = string.Empty
        };

        if (provider == "tmdb" && mediaType == "show")
            series.TMDB_ShowID = providerID;
        else if (provider == "tmdb" && mediaType == "movie")
            series.TMDB_MovieID = providerID;
        else if (provider == "tvdb" && mediaType == "show")
            series.TvdbShowExternalID = providerID;
        else if (provider == "tvdb" && mediaType == "movie")
            series.TvdbMovieExternalID = providerID;

        var grp = _groupCreator.GetOrCreateSingleGroupForSeries(series);
        series.MediaGroupID = grp.MediaGroupID;
        RepoFactory.MediaSeries.Save(series, false, false);

        _logger.LogInformation("Created MediaSeries {SeriesID} for {Provider} {MediaType} ID {ProviderID}", series.MediaSeriesID, provider, mediaType, providerID);
        return series;
    }

    public async Task<(bool, Dictionary<MediaEpisode, UpdateReason>)> CreateAnimeEpisodes(MediaSeries series)
    {
        var anime = series.AniDB_Anime;
        if (anime == null)
            return (false, []);
        var anidbEpisodes = anime.AniDBEpisodes;
        // Cleanup deleted episodes
        var epsToRemove = RepoFactory.MediaEpisode.GetBySeriesID(series.MediaSeriesID)
            .Where(a => a.AniDB_Episode is null)
            .ToList();
        var filesToUpdate = epsToRemove
            .SelectMany(a => a.FileCrossReferences)
            .ToList();
        var vlIDsToUpdate = filesToUpdate
            .Select(a => a.VideoLocal)
            .WhereNotNull()
            .ToList();

        // remove the current release and schedule a recheck for the file if
        // auto match is enabled.
        var scheduler = await _schedulerFactory.GetScheduler();
        foreach (var video in vlIDsToUpdate)
        {
            await _videoReleaseService.ClearReleaseForVideo(video);
            await _videoReleaseService.ScheduleFindReleaseForVideo(video);
        }

        _logger.LogTrace($"Generating {anidbEpisodes.Count} episodes for {anime.MainTitle}");

        var oneForth = (int)Math.Round(anidbEpisodes.Count / 4D, 0, MidpointRounding.AwayFromZero);
        var oneHalf = (int)Math.Round(anidbEpisodes.Count / 2D, 0, MidpointRounding.AwayFromZero);
        var threeFourths = (int)Math.Round(anidbEpisodes.Count * 3 / 4D, 0, MidpointRounding.AwayFromZero);
        var episodeDict = new Dictionary<MediaEpisode, UpdateReason>();
        for (var i = 0; i < anidbEpisodes.Count; i++)
        {
            if (i == oneForth)
            {
                _logger.LogTrace($"Generating episodes for {anime.MainTitle}: 25%");
            }

            if (i == oneHalf)
            {
                _logger.LogTrace($"Generating episodes for {anime.MainTitle}: 50%");
            }

            if (i == threeFourths)
            {
                _logger.LogTrace($"Generating episodes for {anime.MainTitle}: 75%");
            }

            if (i == anidbEpisodes.Count - 1)
            {
                _logger.LogTrace($"Generating episodes for {anime.MainTitle}: 100%");
            }

            var MetadataEpisode = anidbEpisodes[i];
            var (dacollectorEpisode, isNew, isUpdated) = CreateAnimeEpisode(MetadataEpisode, series.MediaSeriesID);
            if (isUpdated)
                episodeDict.Add(dacollectorEpisode, isNew ? UpdateReason.Added : UpdateReason.Updated);
        }

        RepoFactory.MediaEpisode.Delete(epsToRemove);

        // Add removed episodes to the dictionary.
        foreach (var episode in epsToRemove)
            episodeDict.Add(episode, UpdateReason.Removed);

        return (
            episodeDict.ContainsValue(UpdateReason.Added) || epsToRemove.Count > 0,
            episodeDict
        );
    }

    private (MediaEpisode episode, bool isNew, bool isUpdated) CreateAnimeEpisode(AniDB_Episode episode, int MediaSeriesID)
    {
        // check if there is an existing episode for this EpisodeID
        var existingEp = RepoFactory.MediaEpisode.GetByAniDBEpisodeID(episode.EpisodeID);
        var isNew = existingEp is null;
        existingEp ??= new();
        if (existingEp.MediaEpisodeID is 0)
            existingEp.DateTimeCreated = existingEp.DateTimeUpdated = DateTime.Now;

        var old = existingEp.DeepClone();
        existingEp.MediaSeriesID = MediaSeriesID;
        existingEp.AniDB_EpisodeID = episode.EpisodeID;

        var updated = !old.Equals(existingEp);
        if (isNew || updated)
            RepoFactory.MediaEpisode.Save(existingEp);

        _userDataService.CreateUserRecordsForNewEpisode(existingEp);

        return (existingEp, isNew, updated);
    }

    public void MoveSeries(MediaSeries series, MediaGroup newGroup, bool updateGroupStats = true, bool updateEvent = true)
    {
        // Skip moving series if it's already part of the group.
        if (series.MediaGroupID == newGroup.MediaGroupID)
            return;

        var oldGroupID = series.MediaGroupID;
        // Update the stats for the series and group.
        series.MediaGroupID = newGroup.MediaGroupID;
        series.DateTimeUpdated = DateTime.Now;
        UpdateStats(series, true, true);
        if (updateGroupStats)
            _groupService.UpdateStatsFromTopLevel(newGroup.TopLevelMediaGroup, true, true);

        var oldGroup = RepoFactory.MediaGroup.GetByID(oldGroupID);
        if (oldGroup is not null)
        {
            // This was the only one series in the group so delete the now orphan group.
            if (oldGroup.AllSeries.Count == 0)
            {
                _groupService.DeleteGroup(oldGroup, false);
            }
            else
            {
                var updatedOldGroup = false;
                if (oldGroup.DefaultMediaSeriesID.HasValue && oldGroup.DefaultMediaSeriesID.Value == series.MediaSeriesID)
                {
                    oldGroup.DefaultMediaSeriesID = null;
                    updatedOldGroup = true;
                }

                if (oldGroup.MainAniDBAnimeID.HasValue && oldGroup.MainAniDBAnimeID.Value == series.AniDB_ID)
                {
                    oldGroup.MainAniDBAnimeID = null;
                    updatedOldGroup = true;
                }

                if (updatedOldGroup)
                    RepoFactory.MediaGroup.Save(oldGroup);
            }

            // Update the top group
            var topGroup = oldGroup.TopLevelMediaGroup;
            if (topGroup.MediaGroupID != oldGroup.MediaGroupID)
            {
                _groupService.UpdateStatsFromTopLevel(topGroup, true, true);
            }
        }

        if (updateEvent)
            DaCollectorEventHandler.Instance.OnSeriesUpdated(series, UpdateReason.Updated);
    }

    public async Task QueueUpdateStats(MediaSeries series)
    {
        if (series == null) return;
        var scheduler = await _schedulerFactory.GetScheduler();
        await scheduler.StartJob<RefreshSeriesStatsJob>(c => c.MediaSeriesID = series.MediaSeriesID);
    }

    public void UpdateStats(MediaSeries? series, bool watchedStats, bool missingEpsStats)
    {
        if (series == null) return;
        lock (this)
        {
            var start = DateTime.Now;
            var initialStart = DateTime.Now;
            var name = series.AniDB_Anime?.MainTitle ?? series.AniDB_ID?.ToString() ?? series.MediaSeriesID.ToString();
            _logger.LogInformation("Starting Updating STATS for SERIES {Name} - Watched Stats: {WatchedStats}, Missing Episodes: {MissingEpsStats}", name,
                watchedStats, missingEpsStats);

            var startEps = DateTime.Now;
            var eps = series.AllAnimeEpisodes;
            var tsEps = DateTime.Now - startEps;
            _logger.LogTrace("Got episodes for SERIES {Name} in {Elapsed}ms", name, tsEps.TotalMilliseconds);

            // Ensure the episode added date is accurate.
            if (series.AniDB_ID is not null and not 0)
                series.EpisodeAddedDate = RepoFactory.StoredReleaseInfo.GetByAnidbAnimeID(series.AniDB_ID.Value)
                    .Select(a => a.LastUpdatedAt)
                    .DefaultIfEmpty()
                    .Max();

            if (watchedStats) UpdateWatchedStats(series, eps, name, ref start);
            if (missingEpsStats) UpdateMissingEpisodeStats(series, eps, name, ref start);

            // Skip group filters if we are doing group stats, as the group stats will regenerate group filters
            RepoFactory.MediaSeries.Save(series, false, false);
            var ts = DateTime.Now - start;
            _logger.LogTrace("Saved stats for SERIES {Name} in {Elapsed}ms", name, ts.TotalMilliseconds);

            ts = DateTime.Now - initialStart;
            _logger.LogInformation("Finished updating stats for SERIES {Name} in {Elapsed}ms", name, ts.TotalMilliseconds);
        }
    }

    private void UpdateWatchedStats(MediaSeries series, IReadOnlyList<MediaEpisode> eps, string name, ref DateTime start)
    {
        if (eps.Count == 0 && series.AniDB_ID is null or 0)
            _userDataService.UpdateWatchedStatsTmdbNative(series);
        else
            _userDataService.UpdateWatchedStats(series, eps);
        var ts = DateTime.Now - start;
        _logger.LogTrace("Updated WATCHED stats for SERIES {Name} in {Elapsed}ms", name, ts.TotalMilliseconds);
        start = DateTime.Now;
    }

    private void UpdateMissingEpisodeStatsTmdbNative(MediaSeries series, IReadOnlyList<MediaEpisode> eps)
    {
        var latestLocalEpNumber = 0;
        DateTime? lastEpAirDate = null;
        var today = DateOnly.FromDateTime(DateTime.Today);

        if (eps.Count > 0)
        {
            foreach (var ep in eps.Where(e => e.EpisodeType == EpisodeType.Episode))
            {
                var airDate = ((IEpisode)ep).AirDate;
                if (airDate is null || airDate.Value > today)
                    continue;

                var vids = ep.VideoLocals;
                var hasFile = vids.Count > 0;
                var epNum = ((IEpisode)ep).EpisodeNumber;

                if (hasFile && epNum > latestLocalEpNumber)
                    latestLocalEpNumber = epNum;

                var airDateTime = airDate.Value.ToDateTime(TimeOnly.MinValue);
                if (lastEpAirDate is null || airDateTime > lastEpAirDate)
                    lastEpAirDate = airDateTime;

                if (!hasFile)
                {
                    if (ep.IsHidden)
                        series.HiddenMissingEpisodeCount++;
                    else
                        series.MissingEpisodeCount++;
                }
            }

            series.LatestLocalEpisodeNumber = latestLocalEpNumber;
            series.LatestEpisodeAirDate = lastEpAirDate ?? series.AirDate;
            return;
        }

        // No MediaEpisode records — compute stats directly from provider data.
        if (series.TMDB_ShowID.HasValue)
        {
            foreach (var ep in RepoFactory.TMDB_Episode.GetByTmdbShowID(series.TMDB_ShowID.Value)
                         .Where(e => e.SeasonNumber > 0))
            {
                var airDate = ep.AiredAt;
                if (airDate is null || airDate.Value > today)
                    continue;

                var hasFile = RepoFactory.CrossRef_File_TmdbEpisode.GetByTmdbEpisodeID(ep.TmdbEpisodeID).Count > 0;
                if (hasFile && ep.EpisodeNumber > latestLocalEpNumber)
                    latestLocalEpNumber = ep.EpisodeNumber;

                var airDateTime = airDate.Value.ToDateTime(TimeOnly.MinValue);
                if (lastEpAirDate is null || airDateTime > lastEpAirDate)
                    lastEpAirDate = airDateTime;

                if (!hasFile)
                {
                    if (ep.IsHidden)
                        series.HiddenMissingEpisodeCount++;
                    else
                        series.MissingEpisodeCount++;
                }
            }
        }
        else if (series.TvdbShowExternalID.HasValue)
        {
            foreach (var ep in RepoFactory.TVDB_Episode.GetByTvdbShowID(series.TvdbShowExternalID.Value)
                         .Where(e => e.SeasonNumber > 0))
            {
                var airDate = ep.AiredAt;
                if (airDate is null || airDate.Value > today)
                    continue;

                var hasFile = RepoFactory.CrossRef_File_TvdbEpisode.GetByTvdbEpisodeID(ep.TvdbEpisodeID).Count > 0;
                if (hasFile && ep.EpisodeNumber > latestLocalEpNumber)
                    latestLocalEpNumber = ep.EpisodeNumber;

                var airDateTime = airDate.Value.ToDateTime(TimeOnly.MinValue);
                if (lastEpAirDate is null || airDateTime > lastEpAirDate)
                    lastEpAirDate = airDateTime;

                if (!hasFile)
                    series.MissingEpisodeCount++;
            }
        }

        series.LatestLocalEpisodeNumber = latestLocalEpNumber;
        series.LatestEpisodeAirDate = lastEpAirDate ?? series.AirDate;
    }

    private void UpdateMissingEpisodeStats(MediaSeries series, IReadOnlyList<MediaEpisode> eps, string name, ref DateTime start)
    {
        series.MissingEpisodeCount = 0;
        series.MissingEpisodeCountGroups = 0;
        series.HiddenMissingEpisodeCount = 0;
        series.HiddenMissingEpisodeCountGroups = 0;

        if (series.AniDB_ID is null or 0)
        {
            UpdateMissingEpisodeStatsTmdbNative(series, eps);
            var ts0 = DateTime.Now - start;
            _logger.LogTrace("Updated MISSING EPS stats for SERIES {Name} in {Elapsed}ms", name, ts0.TotalMilliseconds);
            start = DateTime.Now;
            return;
        }

        var mediaType = series.AniDB_Anime?.MediaType ?? MediaType.TVSeries;

        // get all the group status records
        var grpStatuses = RepoFactory.AniDB_GroupStatus.GetByAnimeID(series.AniDB_ID.Value);

        // find all the episodes for which the user has a file
        // from this we can determine what their latest episode number is
        // find out which groups the user is collecting

        var latestLocalEpNumber = 0;
        DateTime? lastEpAirDate = null;
        var epReleasedList = new EpisodeList(mediaType);
        var epGroupReleasedList = new EpisodeList(mediaType);
        var daysofweekcounter = new Dictionary<DayOfWeek, int>();

        var userReleaseGroups = eps
            .Where(a => a.EpisodeType == EpisodeType.Episode)
            .SelectMany(a => a.VideoLocals
                .Select(b => b.ReleaseGroup)
                .WhereNotNull()
                .Where(b => b.Source is "AniDB" && int.TryParse(b.ID, out var groupID) && groupID > 0)
                .Select(b => int.Parse(b.ID))
            )
            .Distinct()
            .ToList();

        var videoLocals = eps.Where(a => a.EpisodeType == EpisodeType.Episode).SelectMany(a =>
                a.VideoLocals.Select(b => new
                {
                    a.AniDB_EpisodeID,
                    VideoLocal = b
                }))
            .ToLookup(a => a.AniDB_EpisodeID, a => a.VideoLocal);

        // This was always Episodes only. Maybe in the future, we'll have a reliable way to check specials.
        eps.AsParallel().Where(a => a.EpisodeType == EpisodeType.Episode).ForAll(ep =>
        {
            var aniEp = ep.AniDB_Episode;
            // Un-aired episodes should not be included in the stats.
            if (aniEp is not { HasAired: true }) return;

            var thisEpNum = aniEp.EpisodeNumber;
            // does this episode have a file released
            var epReleased = false;
            // does this episode have a file released by the group the user is collecting
            var epReleasedGroup = false;

            if (grpStatuses.Count == 0)
            {
                // If there are no group statuses, the UDP command has not been run yet or has failed
                // The current has aired, as un-aired episodes are filtered out above
                epReleased = true;
                // We do not set epReleasedGroup here because we have no way to know
            }
            else
            {
                // Get all groups which have their status set to complete or finished or have released this episode
                var filteredGroups = grpStatuses
                    .Where(
                        a => a.CompletionState is 3 or 5 // Complete or Finished
                             || a.HasGroupReleasedEpisode(thisEpNum))
                    .ToList();
                // Episode is released if any of the groups have released it
                epReleased = filteredGroups.Count > 0;
                // Episode is released by one of the groups user is collecting if one of the userReleaseGroups is included in filteredGroups
                epReleasedGroup = filteredGroups.Any(a => userReleaseGroups.Contains(a.GroupID));
            }

            // If epReleased is false, then we consider the episode to be not released, even if it has aired, as no group has released it
            if (!epReleased) return;

            var vids = videoLocals[ep.AniDB_EpisodeID].ToList();

            if (thisEpNum > latestLocalEpNumber && vids.Any())
            {
                latestLocalEpNumber = thisEpNum;
            }

            var airdate = ep.AniDB_Episode?.GetAirDateAsDate();

            // If episode air date is unknown, air date of the anime is used instead
            airdate ??= series.AniDB_Anime?.AirDate;

            // Only count episodes that have already aired
            // airdate could, in theory, only be null here if AniDB neither has information on the episode
            // air date, nor on the anime air date. luckily, as of 2024-07-09, no such case exists.
            if (aniEp.HasAired && airdate is not null)
            {
                // Only convert if we have time info
                DateTime airdateLocal;
                if (airdate.Value.Hour == 0 && airdate.Value.Minute == 0 && airdate.Value.Second == 0)
                {
                    airdateLocal = airdate.Value;
                }
                else
                {
                    airdateLocal = DateTime.SpecifyKind(airdate.Value, DateTimeKind.Unspecified);
                    airdateLocal = TimeZoneInfo.ConvertTime(airdateLocal,
                        TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time"), TimeZoneInfo.Local);
                }

                lock (daysofweekcounter)
                {
                    daysofweekcounter.TryAdd(airdateLocal.DayOfWeek, 0);
                    daysofweekcounter[airdateLocal.DayOfWeek]++;
                }

                if (lastEpAirDate == null || lastEpAirDate < airdate)
                {
                    lastEpAirDate = airdate.Value;
                }
            }

            try
            {
                lock (epReleasedList)
                {
                    epReleasedList.Add(ep, vids.Count > 0);
                }

                // Skip adding to epGroupReleasedList if the episode has not been released by one of the groups user is collecting
                if (!epReleasedGroup) return;

                lock (epGroupReleasedList)
                {
                    epGroupReleasedList.Add(ep, vids.Count > 0);
                }
            }
            catch (Exception e)
            {
                _logger.LogTrace(e, "Error updating release group stats {Ex}", e);
                throw;
            }
        });

        foreach (var eplst in epReleasedList)
        {
            if (eplst.Available) continue;

            if (eplst.Hidden)
                series.HiddenMissingEpisodeCount++;
            else
                series.MissingEpisodeCount++;
        }

        foreach (var eplst in epGroupReleasedList)
        {
            if (eplst.Available) continue;

            if (eplst.Hidden)
                series.HiddenMissingEpisodeCountGroups++;
            else
                series.MissingEpisodeCountGroups++;
        }

        series.LatestLocalEpisodeNumber = latestLocalEpNumber;
        if (daysofweekcounter.Count > 0)
        {
            series.AirsOn = daysofweekcounter.OrderByDescending(a => a.Value).FirstOrDefault().Key;
        }

        series.LatestEpisodeAirDate = lastEpAirDate;

        var ts = DateTime.Now - start;
        _logger.LogTrace("Updated MISSING EPS stats for SERIES {Name} in {Elapsed}ms", name, ts.TotalMilliseconds);
        start = DateTime.Now;
    }

    public Dictionary<MediaSeries, AniDB_Anime_Staff> SearchSeriesByStaff(string staffName, bool fuzzy = false)
    {
        var allSeries = RepoFactory.MediaSeries.GetAll();
        var results = new Dictionary<MediaSeries, AniDB_Anime_Staff>();
        var stringsToSearchFor = new List<string>();
        if (staffName.Contains(' '))
        {
            stringsToSearchFor.AddRange(staffName.Split(' ').GetPermutations()
                .Select(permutation => string.Join(" ", permutation)));
            stringsToSearchFor.Remove(staffName);
            stringsToSearchFor.Insert(0, staffName);
        }
        else
        {
            stringsToSearchFor.Add(staffName);
        }

        foreach (var series in allSeries)
        {
            foreach (var (xref, staff) in RepoFactory.AniDB_Anime_Staff.GetByAnimeID(series.AniDB_ID ?? 0).Select(a => (a, a.Creator)))
            {
                if (staff is null)
                    continue;

                foreach (var search in stringsToSearchFor)
                {
                    if (fuzzy)
                    {
                        if (!staff.Name.FuzzyMatch(search))
                        {
                            continue;
                        }
                    }
                    else
                    {
                        if (!staff.Name.Equals(search, StringComparison.InvariantCultureIgnoreCase))
                        {
                            continue;
                        }
                    }

                    if (!results.TryAdd(series, xref))
                    {
                        var comparison = ((int)results[series].RoleType).CompareTo((int)xref.RoleType);
                        if (comparison == 1)
                            results[series] = xref;
                    }

                    goto label0;
                }
            }

            // People hate goto, but this is a legit use for it.
            label0:;
        }

        return results;
    }

    public async Task DeleteSeries(MediaSeries series, bool deleteFiles, bool updateGroups, bool completelyRemove = false, bool removeFromMylist = true)
    {
        var service = Utils.ServiceContainer.GetRequiredService<IVideoService>();
        foreach (var ep in series.AllAnimeEpisodes)
        {
            foreach (var place in series.VideoLocals.SelectMany(a => a.Places).WhereNotNull())
                await service.DeleteVideoFile(place, removeFile: deleteFiles);

            RepoFactory.MediaEpisode.Delete(ep.MediaEpisodeID);
        }
        RepoFactory.MediaSeries.Delete(series);

        if (!updateGroups)
        {
            return;
        }

        // finally update stats
        var grp = series.MediaGroup;
        if (grp is not null)
        {
            if (!grp.AllSeries.Any())
            {
                // Find the topmost group without series
                var parent = grp;
                while (true)
                {
                    var next = parent.Parent;
                    if (next == null || next.AllSeries.Any())
                    {
                        break;
                    }

                    parent = next;
                }

                _groupService.DeleteGroup(parent);
            }
            else
            {
                _groupService.UpdateStatsFromTopLevel(grp, true, true);
            }
        }

        DaCollectorEventHandler.Instance.OnSeriesUpdated(series, UpdateReason.Removed);

        if (completelyRemove)
        {
            // episodes, anime, characters, images, staff relations, tag relations, titles
            var images = RepoFactory.AniDB_Anime_PreferredImage.GetByAnimeID(series.AniDB_ID ?? 0);
            RepoFactory.AniDB_Anime_PreferredImage.Delete(images);

            var characterXrefs = RepoFactory.AniDB_Anime_Character.GetByAnimeID(series.AniDB_ID ?? 0);
            var characters = characterXrefs
                .Select(x => x.Character)
                .WhereNotNull()
                .Where(x => !RepoFactory.AniDB_Anime_Character.GetByCharacterID(x.CharacterID).ExceptBy(characterXrefs.Select(y => y.AniDB_Anime_CharacterID), y => y.AniDB_Anime_CharacterID).Any())
                .ToList();
            RepoFactory.AniDB_Anime_Character.Delete(characterXrefs);
            RepoFactory.AniDB_Character.Delete(characters);

            var actorXrefs = RepoFactory.AniDB_Anime_Character_Creator.GetByAnimeID(series.AniDB_ID ?? 0);
            var staffXrefs = RepoFactory.AniDB_Anime_Staff.GetByAnimeID(series.AniDB_ID ?? 0);
            var creators = actorXrefs.Select(x => x.Creator)
                .Concat(staffXrefs.Select(x => x.Creator))
                .WhereNotNull()
                .Where(x =>
                    !x.Staff.ExceptBy(staffXrefs.Select(y => y.AniDB_Anime_StaffID), y => y.AniDB_Anime_StaffID).Any() &&
                    !x.Characters.ExceptBy(actorXrefs.Select(y => y.AniDB_Anime_Character_CreatorID), y => y.AniDB_Anime_Character_CreatorID).Any()
                )
                .ToList();
            RepoFactory.AniDB_Anime_Character_Creator.Delete(actorXrefs);
            RepoFactory.AniDB_Anime_Staff.Delete(staffXrefs);
            RepoFactory.AniDB_Creator.Delete(creators);

            var tagXrefs = RepoFactory.AniDB_Anime_Tag.GetByAnimeID(series.AniDB_ID ?? 0);
            RepoFactory.AniDB_Anime_Tag.Delete(tagXrefs);

            var titles = RepoFactory.AniDB_Anime_Title.GetByAnimeID(series.AniDB_ID ?? 0);
            RepoFactory.AniDB_Anime_Title.Delete(titles);

            var aniDBEpisodes = RepoFactory.AniDB_Episode.GetByAnimeID(series.AniDB_ID ?? 0);
            var episodeTitles = aniDBEpisodes.SelectMany(a => RepoFactory.AniDB_Episode_Title.GetByEpisodeID(a.EpisodeID)).ToList();
            RepoFactory.AniDB_Episode_Title.Delete(episodeTitles);
            RepoFactory.AniDB_Episode.Delete(aniDBEpisodes);

            var update = RepoFactory.AniDB_AnimeUpdate.GetByAnimeID(series.AniDB_ID ?? 0);
            RepoFactory.AniDB_AnimeUpdate.Delete(update);

            // remove all releases linked to this series
            var releases = RepoFactory.StoredReleaseInfo.GetByAnidbAnimeID(series.AniDB_ID ?? 0);
            foreach (var release in releases)
                await _videoReleaseService.RemoveRelease(release, removeFromMylist);
        }
    }

    /// <summary>
    /// Get the most recent actively watched episode for the user.
    /// </summary>
    /// <param name="series"></param>
    /// <param name="userID">User ID</param>
    /// <param name="includeSpecials">Include specials when searching.</param>
    /// <param name="includeOthers">Include other type episodes when searching.</param>
    /// <returns></returns>
    public MediaEpisode GetActiveEpisode(MediaSeries series, int userID, bool includeSpecials = true, bool includeOthers = false)
    {
        // Filter the episodes to only normal or special episodes and order them in rising order.
        var order = includeOthers ? new List<EpisodeType> { EpisodeType.Episode, EpisodeType.Other, EpisodeType.Special } : null;
        var episodes = series.AnimeEpisodes
            .Select(e => (episode: e, e.AniDB_Episode))
            .Where(tuple =>
                tuple.AniDB_Episode?.EpisodeType is EpisodeType.Episode ||
                (includeSpecials && tuple.AniDB_Episode?.EpisodeType is EpisodeType.Special) ||
                (includeOthers && tuple.AniDB_Episode?.EpisodeType is EpisodeType.Other)
            )
            .OrderBy(tuple => order?.IndexOf(tuple.AniDB_Episode!.EpisodeType) ?? (int?)tuple.AniDB_Episode?.EpisodeType)
            .ThenBy(tuple => tuple.AniDB_Episode?.EpisodeNumber)
            .Select(tuple => tuple.episode)
            .ToList();
        // Look for active watch sessions and return the episode for the most recent session if found.
        var (episode, _) = episodes
            .SelectMany(episode => episode.VideoLocals.Select(file => (episode, vlUser: _vlUsers.GetByUserAndVideoLocalID(userID, file.VideoLocalID)!)))
            .Where(tuple => tuple.vlUser is not null)
            .OrderByDescending(tuple => tuple.vlUser.LastUpdated)
            .FirstOrDefault(tuple => tuple.vlUser.ResumePosition > 0);
        return episode;
    }

    #region Next-up Episode(s)

    /// <summary>
    /// Series next-up query options for use with <see cref="GetNextUpEpisode"/>.
    /// </summary>
    public class NextUpQuerySingleOptions : NextUpQueryOptions
    {
        /// <summary>
        /// Disable the first episode in the series from showing up.
        /// /// </summary>
        public bool DisableFirstEpisode { get; set; } = false;

        public NextUpQuerySingleOptions() { }

        public NextUpQuerySingleOptions(NextUpQueryOptions options)
        {
            IncludeCurrentlyWatching = options.IncludeCurrentlyWatching;
            IncludeMissing = options.IncludeMissing;
            IncludeUnaired = options.IncludeUnaired;
            IncludeRewatching = options.IncludeRewatching;
            IncludeSpecials = options.IncludeSpecials;
            IncludeOthers = options.IncludeOthers;
        }
    }

    /// <summary>
    /// Series next-up query options for use with <see cref="GetNextUpEpisode"/>.
    /// </summary>
    public class NextUpQueryOptions
    {
        /// <summary>
        /// Include currently watching episodes in the search.
        /// </summary>
        public bool IncludeCurrentlyWatching { get; set; } = false;

        /// <summary>
        /// Include missing episodes in the search.
        /// </summary>
        public bool IncludeMissing { get; set; } = false;

        /// <summary>
        /// Include unaired episodes in the search.
        /// </summary>
        public bool IncludeUnaired { get; set; } = false;

        /// <summary>
        /// Include already watched episodes in the search if we determine the
        /// user is "re-watching" the series.
        /// </summary>
        public bool IncludeRewatching { get; set; } = false;

        /// <summary>
        /// Include specials in the search.
        /// </summary>
        public bool IncludeSpecials { get; set; } = true;

        /// <summary>
        /// Include other type episodes in the search.
        /// </summary>
        public bool IncludeOthers { get; set; } = false;
    }

    /// <summary>
    /// Get the next episode for the series for a user.
    /// </summary>
    /// <param name="series"></param>
    /// <param name="userID">User ID</param>
    /// <param name="options">Next-up query options.</param>
    /// <returns></returns>
    public MediaEpisode? GetNextUpEpisode(MediaSeries series, int userID, NextUpQuerySingleOptions options)
    {
        var episodeList = series.AnimeEpisodes
            .Select(dacollector => (dacollector, anidb: dacollector.AniDB_Episode!))
            .Where(tuple =>
                tuple.anidb is not null && (
                    (tuple.anidb.EpisodeType is EpisodeType.Episode) ||
                    (options.IncludeSpecials && tuple.anidb.EpisodeType is EpisodeType.Special) ||
                    (options.IncludeOthers && tuple.anidb.EpisodeType is EpisodeType.Other)
                )
            )
            .ToList();

        // Look for active watch sessions and return the episode for the most
        // recent session if found.
        if (options.IncludeCurrentlyWatching)
        {
            var (currentlyWatchingEpisode, _) = episodeList
                .SelectMany(tuple => tuple.dacollector.VideoLocals.Select(file => (tuple.dacollector, fileUR: _vlUsers.GetByUserAndVideoLocalID(userID, file.VideoLocalID))))
                .Where(tuple => tuple.fileUR is not null)
                .OrderByDescending(tuple => tuple.fileUR!.LastUpdated)
                .FirstOrDefault(tuple => tuple.fileUR!.ResumePosition > 0);

            if (currentlyWatchingEpisode is not null)
                return currentlyWatchingEpisode;
        }
        // Skip check if there is an active watch session for the series, and we
        // don't allow active watch sessions.
        else if (episodeList.Any(tuple => tuple.dacollector.VideoLocals.Any(file => (_vlUsers.GetByUserAndVideoLocalID(userID, file.VideoLocalID)?.ResumePosition ?? 0) > 0)))
        {
            return null;
        }

        // If we're listing out other type episodes, then they should be listed
        // before specials, so order them now.
        if (options.IncludeOthers)
        {
            var order = new List<EpisodeType>() { EpisodeType.Episode, EpisodeType.Other, EpisodeType.Special };
            episodeList = episodeList
                .OrderBy(tuple => order.IndexOf(tuple.anidb.EpisodeType))
                .ThenBy(tuple => tuple.anidb.EpisodeNumber)
                .ToList();
        }

        // When "re-watching" we look for the next episode after the last
        // watched episode.
        if (options.IncludeRewatching)
        {
            var (lastWatchedEpisode, _) = episodeList
                .SelectMany(tuple => tuple.dacollector.VideoLocals.Select(file => (tuple.dacollector, fileUR: _vlUsers.GetByUserAndVideoLocalID(userID, file.VideoLocalID))))
                .Where(tuple => tuple.fileUR is { WatchedDate: not null })
                .OrderByDescending(tuple => tuple.fileUR!.LastUpdated)
                .FirstOrDefault();

            if (lastWatchedEpisode is not null)
            {
                // Return `null` if we're on the last episode in the list.
                var nextIndex = episodeList.FindIndex(tuple => tuple.dacollector.MediaEpisodeID == lastWatchedEpisode.MediaEpisodeID) + 1;
                if (nextIndex == episodeList.Count)
                    return null;

                var (nextEpisode, _) = episodeList
                    .Skip(nextIndex)
                    .FirstOrDefault(options.IncludeUnaired ? _ => true : options.IncludeMissing ? tuple => tuple.anidb.HasAired || tuple.dacollector.VideoLocals.Count > 0 : tuple => tuple.dacollector.VideoLocals.Count > 0);
                return nextEpisode;
            }
        }

        // Find the first episode that's unwatched.
        var (unwatchedEpisode, MetadataEpisode) = episodeList
            .Where(tuple =>
            {
                var episodeUserRecord = tuple.dacollector.GetUserRecord(userID);
                if (episodeUserRecord is null)
                    return true;

                return !episodeUserRecord.WatchedDate.HasValue;
            })
            .FirstOrDefault(options.IncludeUnaired ? _ => true : options.IncludeMissing ? tuple => tuple.anidb.HasAired || tuple.dacollector.VideoLocals.Count > 0 : tuple => tuple.dacollector.VideoLocals.Count > 0);

        // Disable first episode from showing up in the search.
        if (options.DisableFirstEpisode && MetadataEpisode is not null && MetadataEpisode.EpisodeType is EpisodeType.Episode && MetadataEpisode.EpisodeNumber == 1)
            return null;

        return unwatchedEpisode;
    }

    public IReadOnlyList<MediaEpisode> GetNextUpEpisodes(MediaSeries series, int userID, NextUpQueryOptions options)
    {
        var firstEpisode = GetNextUpEpisode(series, userID, new(options));
        if (firstEpisode is null)
            return [];

        var order = new List<EpisodeType>() { EpisodeType.Episode, EpisodeType.Other, EpisodeType.Special };
        var allEpisodes = series.AnimeEpisodes
            .Select(dacollector => (dacollector, anidb: dacollector.AniDB_Episode!))
            .Where(tuple =>
                tuple.anidb is not null && (
                    (tuple.anidb.EpisodeType is EpisodeType.Episode) ||
                    (options.IncludeSpecials && tuple.anidb.EpisodeType is EpisodeType.Special) ||
                    (options.IncludeOthers && tuple.anidb.EpisodeType is EpisodeType.Other)
                )
            )
            .Where(options.IncludeUnaired ? _ => true : options.IncludeMissing ? tuple => tuple.anidb.HasAired || tuple.dacollector.VideoLocals.Count > 0 : tuple => tuple.dacollector.VideoLocals.Count > 0)
            .OrderBy(tuple => order.IndexOf(tuple.anidb.EpisodeType))
            .ThenBy(tuple => tuple.anidb.EpisodeNumber)
            .ToList();
        var index = allEpisodes.FindIndex(tuple => tuple.dacollector.MediaEpisodeID == firstEpisode.MediaEpisodeID);
        if (index == -1)
            return [];

        return allEpisodes
            .Skip(index)
            .Select(tuple => tuple.dacollector)
            .ToList();
    }

    #endregion

    internal class EpisodeList : List<EpisodeList.StatEpisodes>
    {
        public EpisodeList(MediaType ept)
        {
            MediaType = ept;
        }

        private MediaType MediaType { get; set; }

        private readonly Regex partmatch = new("part (\\d.*?) of (\\d.*)");

        private readonly Regex remsymbols = new("[^A-Za-z0-9 ]");

        private readonly Regex remmultispace = new("\\s+");

        public void Add(MediaEpisode ep, bool available)
        {
            if (MediaType == MediaType.OVA || MediaType == MediaType.Movie)
            {
                var ename = ep.Title;
                var empty = string.IsNullOrEmpty(ename);
                Match? m = null;
                if (!empty)
                {
                    m = partmatch.Match(ename);
                }

                var s = new StatEpisodes.StatEpisode { Available = available, Episode = ep };
                if (m?.Success ?? false)
                {
                    int.TryParse(m.Groups[1].Value, out var _);
                    int.TryParse(m.Groups[2].Value, out var part_count);
                    var rname = partmatch.Replace(ename, string.Empty);
                    rname = remsymbols.Replace(rname, string.Empty);
                    rname = remmultispace.Replace(rname, " ");


                    s.EpisodeType = StatEpisodes.StatEpisode.EpType.Part;
                    s.PartCount = part_count;
                    s.Match = rname.Trim();
                    if (s.Match == "complete movie" || s.Match == "movie" || s.Match == "ova")
                    {
                        s.Match = string.Empty;
                    }
                }
                else
                {
                    if (empty || ename == "complete movie" || ename == "movie" || ename == "ova")
                    {
                        s.Match = string.Empty;
                    }
                    else
                    {
                        var rname = partmatch.Replace(ep.Title, string.Empty);
                        rname = remsymbols.Replace(rname, string.Empty);
                        rname = remmultispace.Replace(rname, " ");
                        s.Match = rname.Trim();
                    }

                    s.EpisodeType = StatEpisodes.StatEpisode.EpType.Complete;
                    s.PartCount = 0;
                }

                StatEpisodes? fnd = null;
                foreach (var k in this)
                {
                    if (k.Any(ss => ss.Match == s.Match)) fnd = k;
                    if (fnd is not null) break;
                }

                if (fnd == null)
                {
                    var eps = new StatEpisodes();
                    eps.Add(s);
                    Add(eps);
                }
                else
                {
                    fnd.Add(s);
                }
            }
            else
            {
                var eps = new StatEpisodes();
                var es = new StatEpisodes.StatEpisode
                {
                    Match = string.Empty,
                    EpisodeType = StatEpisodes.StatEpisode.EpType.Complete,
                    PartCount = 0,
                    Available = available,
                    Episode = ep,
                };
                eps.Add(es);
                Add(eps);
            }
        }

        public class StatEpisodes : List<StatEpisodes.StatEpisode>
        {
            public class StatEpisode
            {
                public enum EpType
                {
                    Complete,
                    Part
                }

                public string? Match { get; set; }
                public int PartCount { get; set; }
                public EpType EpisodeType { get; set; }
                public required bool Available { get; set; }
                public required MediaEpisode Episode { get; set; }
            }

            public bool Available
            {
                get
                {
                    var maxcnt = this.Select(k => k.PartCount).Concat(new[] { 0 }).Max();
                    var parts = new int[maxcnt + 1];
                    foreach (var k in this)
                    {
                        switch (k.EpisodeType)
                        {
                            case StatEpisode.EpType.Complete when k.Available:
                                return true;
                            case StatEpisode.EpType.Part when k.Available:
                                parts[k.PartCount]++;
                                if (parts[k.PartCount] == k.PartCount)
                                {
                                    return true;
                                }

                                break;
                        }
                    }

                    return false;
                }
            }

            public bool Hidden
                => this.Any(e => e.Episode.IsHidden);
        }
    }
}
