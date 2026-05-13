using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using DaCollector.Abstractions.Extensions;
using DaCollector.Server.Databases;
using DaCollector.Server.Extensions;
using DaCollector.Server.Models.AniDB;
using DaCollector.Server.Models.DaCollector;
using DaCollector.Server.Repositories;
using DaCollector.Server.Repositories.Cached;
using DaCollector.Server.Repositories.Cached.AniDB;
using DaCollector.Server.Repositories.NHibernate;
using DaCollector.Server.Scheduling;
using DaCollector.Server.Services;
using DaCollector.Server.Settings;

namespace DaCollector.Server.Tasks;

public class MediaGroupCreator
{
    private readonly ILogger<MediaGroupCreator> _logger;
    private const int DefaultBatchSize = 50;
    public const string TempGroupName = "AAA Migrating Groups AAA";
    private static readonly Regex _truncateYearRegex = new(@"\s*\(\d{4}\)$");
    private readonly SystemService _systemService;
    private readonly QueueHandler _queueHandler;
    private readonly MediaGroupService _groupService;
    private readonly AniDB_AnimeRepository _aniDbAnimeRepo;
    private readonly MediaSeriesRepository _animeSeriesRepo;
    private readonly MediaGroupRepository _animeGroupRepo;
    private readonly MediaGroup_UserRepository _animeGroupUserRepo;
    private readonly bool _autoGroupSeries;
    private readonly DatabaseFactory _databaseFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="MediaGroupCreator"/> class.
    /// </summary>
    /// <remarks>
    /// Uses the current server configuration to determine if auto grouping series is enabled.
    /// </remarks>
    public MediaGroupCreator(
        SystemService systemService,
        ISettingsProvider settingsProvider,
        QueueHandler queueHandler,
        ILogger<MediaGroupCreator> logger,
        DatabaseFactory databaseFactory,
        AniDB_AnimeRepository aniDbAnimeRepo,
        MediaSeriesRepository animeSeriesRepo,
        MediaGroupRepository animeGroupRepo,
        MediaGroup_UserRepository animeGroupUserRepo,
        MediaGroupService groupService
    )
    {
        _systemService = systemService;
        _queueHandler = queueHandler;
        _logger = logger;
        _databaseFactory = databaseFactory;
        _aniDbAnimeRepo = aniDbAnimeRepo;
        _animeSeriesRepo = animeSeriesRepo;
        _animeGroupRepo = animeGroupRepo;
        _animeGroupUserRepo = animeGroupUserRepo;
        _groupService = groupService;
        _autoGroupSeries = settingsProvider.GetSettings().AutoGroupSeries;
    }

    /// <summary>
    /// Creates a new group that series will be put in during group re-calculation.
    /// </summary>
    /// <param name="session">The NHibernate session.</param>
    /// <returns>The temporary <see cref="MediaGroup"/>.</returns>
    private async Task<MediaGroup> CreateTempAnimeGroup(ISessionWrapper session)
    {
        var now = DateTime.Now;

        var tempGroup = new MediaGroup
        {
            GroupName = TempGroupName,
            Description = TempGroupName,
            DateTimeUpdated = now,
            DateTimeCreated = now
        };

        // We won't use MediaGroupRepository.Save because we don't need to perform all the extra stuff since this is for temporary use only
        await session.InsertAsync(tempGroup);
        lock (_animeGroupRepo.Cache)
        {
            _animeGroupRepo.Cache.Update(tempGroup);
        }

        return tempGroup;
    }

    /// <summary>
    /// Deletes the anime groups and user mappings as well as resetting group filters and moves all anime series into the specified group.
    /// </summary>
    /// <param name="session">The NHibernate session.</param>
    /// <param name="tempGroupId">The ID of the temporary anime group to use for migration.</param>
    private async Task ClearGroupsAndDependencies(ISessionWrapper session, int tempGroupId)
    {
        _logger.LogInformation("Removing existing AnimeGroups and resetting GroupFilters");

        await _animeGroupUserRepo.DeleteAll(session);
        await _animeGroupRepo.DeleteAll(session, tempGroupId);
        await BaseRepository.Lock(async () =>
        {
            await session.CreateSQLQuery(@"
                UPDATE MediaSeries SET MediaGroupID = :tempGroupId;")
                .SetInt32("tempGroupId", tempGroupId)
                .ExecuteUpdateAsync();
        });

        // We've deleted/modified all MediaSeries/GroupFilter records, so update caches to reflect that
        _animeSeriesRepo.ClearCache();
        _logger.LogInformation("AnimeGroups have been removed and GroupFilters have been reset");
    }

    private async Task UpdateAnimeSeriesContractsAndSave(ISessionWrapper session,
        IReadOnlyCollection<MediaSeries> series)
    {
        await _animeSeriesRepo.UpdateBatch(session, series);
        _logger.LogInformation("MediaSeries contracts have been updated");
    }

    private Task UpdateAnimeGroupsAndTheirContracts(IReadOnlyCollection<MediaGroup> groups)
    {
        _logger.LogInformation("Updating statistics for AnimeGroups");
        var allGroupUsers = new ConcurrentBag<List<MediaGroup_User>>();
        Parallel.ForEach(groups.Batch(DefaultBatchSize), new ParallelOptions { MaxDegreeOfParallelism = 4 },
            (groupBatch, _, localSession) =>
            {
                var createdGroupUsers = new List<MediaGroup_User>(groupBatch.Length);
                var updatedGroupUsers = new List<MediaGroup_User>(groupBatch.Length);
                _groupService.BatchUpdateStats(groupBatch, true, true,
                    createdGroupUsers, updatedGroupUsers);
                allGroupUsers.Add(createdGroupUsers);
                allGroupUsers.Add(updatedGroupUsers);
            });
        var animeGroupUsers = allGroupUsers.SelectMany(groupUsers => groupUsers)
            .ToList();
        _animeGroupUserRepo.Save(animeGroupUsers);
        _logger.LogInformation("MediaGroup statistics have been saved");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Creates a single <see cref="MediaGroup"/> for each <see cref="MediaSeries"/> in <paramref name="seriesList"/>.
    /// </summary>
    /// <param name="session"></param>
    /// <param name="seriesList">The list of <see cref="MediaSeries"/> to create groups for.</param>
    /// <returns>A sequence of the created <see cref="MediaGroup"/>s.</returns>
    private async Task<IEnumerable<MediaGroup>> CreateGroupPerSeries(ISessionWrapper session, IReadOnlyList<MediaSeries> seriesList)
    {
        _logger.LogInformation("Generating AnimeGroups for {Count} MediaSeries", seriesList.Count);

        var now = DateTime.Now;
        var newGroupsToSeries = new Tuple<MediaGroup, MediaSeries>[seriesList.Count];

        // Create one group per series
        for (var grp = 0; grp < seriesList.Count; grp++)
        {
            var group = new MediaGroup();
            var series = seriesList[grp];

            group.Populate(series, now);
            newGroupsToSeries[grp] = new Tuple<MediaGroup, MediaSeries>(group, series);
        }

        await BaseRepository.Lock(async () => await _animeGroupRepo.InsertBatch(session, newGroupsToSeries.Select(gts => gts.Item1).AsReadOnlyCollection()));

        // Anime groups should have IDs now they've been inserted. Now assign the group ID's to their respective series
        // (The caller of this method will be responsible for saving the MediaSeries)
        foreach (var groupAndSeries in newGroupsToSeries)
        {
            groupAndSeries.Item2.MediaGroupID = groupAndSeries.Item1.MediaGroupID;
        }

        _logger.LogInformation("Generated {Count} AnimeGroups", newGroupsToSeries.Length);

        return newGroupsToSeries.Select(gts => gts.Item1);
    }

    /// <summary>
    /// Creates <see cref="MediaGroup"/> that contain <see cref="MediaSeries"/> that appear to be related.
    /// </summary>
    /// <remarks>
    /// This method assumes that there are no active transactions on the specified <paramref name="session"/>.
    /// </remarks>
    /// <param name="session"></param>
    /// <param name="seriesList">The list of <see cref="MediaSeries"/> to create groups for.</param>
    /// <returns>A sequence of the created <see cref="MediaGroup"/>s.</returns>
    private async Task<IEnumerable<MediaGroup>> AutoCreateGroupsWithRelatedSeries(ISessionWrapper session, IReadOnlyCollection<MediaSeries> seriesList)
    {
        _logger.LogInformation("Auto-generating AnimeGroups for {Count} MediaSeries based on aniDB relationships", seriesList.Count);

        var now = DateTime.Now;
        var grpCalculator = AutoAnimeGroupCalculator.CreateFromServerSettings();

        _logger.LogInformation("The following exclusions will be applied when generating the groups: {Exclusions}", grpCalculator.Exclusions);

        // Group all of the specified series into their respective groups (keyed by the groups main anime ID)
        var seriesByGroup = seriesList.ToLookup(s => grpCalculator.GetGroupAnimeId(s.AniDB_ID ?? 0));
        var newGroupsToSeries =
            new List<Tuple<MediaGroup, IReadOnlyCollection<MediaSeries>>>(seriesList.Count);

        foreach (var groupAndSeries in seriesByGroup)
        {
            var mainAnimeId = groupAndSeries.Key;
            var mainSeries = groupAndSeries.FirstOrDefault(series => series.AniDB_ID == mainAnimeId);
            var MediaGroup = CreateAnimeGroup(mainSeries, mainAnimeId, now);

            newGroupsToSeries.Add(
                new Tuple<MediaGroup, IReadOnlyCollection<MediaSeries>>(MediaGroup,
                    groupAndSeries.AsReadOnlyCollection()));
        }

        await BaseRepository.Lock(async () => await _animeGroupRepo.InsertBatch(session, newGroupsToSeries.Select(gts => gts.Item1).AsReadOnlyCollection()));

        // Anime groups should have IDs now they've been inserted. Now assign the group ID's to their respective series
        // (The caller of this method will be responsible for saving the MediaSeries)
        foreach (var groupAndSeries in newGroupsToSeries)
        {
            foreach (var series in groupAndSeries.Item2)
            {
                series.MediaGroupID = groupAndSeries.Item1.MediaGroupID;
            }
        }

        _logger.LogInformation("Generated {Count} AnimeGroups", newGroupsToSeries.Count);

        return newGroupsToSeries.Select(gts => gts.Item1);
    }

    /// <summary>
    /// Creates an <see cref="MediaGroup"/> instance.
    /// </summary>
    /// <remarks>
    /// This method only creates an <see cref="MediaGroup"/> instance. It does NOT save it to the database.
    /// </remarks>
    /// <param name="mainSeries">The <see cref="MediaSeries"/> whose name will represent the group (Optional. Pass <c>null</c> if not available).</param>
    /// <param name="mainAnimeId">The ID of the anime whose name will represent the group if <paramref name="mainSeries"/> is <c>null</c>.</param>
    /// <param name="now">The current date/time.</param>
    /// <returns>The created <see cref="MediaGroup"/>.</returns>
    private MediaGroup CreateAnimeGroup(MediaSeries mainSeries, int mainAnimeId,
        DateTime now)
    {
        var MediaGroup = new MediaGroup();
        string groupName;

        if (mainSeries != null)
        {
            MediaGroup.Populate(mainSeries, now);
            groupName = MediaGroup.GroupName;
        }
        else // The anime chosen as the group's main anime doesn't actually have a series
        {
            var mainAnime = _aniDbAnimeRepo.GetByAnimeID(mainAnimeId);

            MediaGroup.Populate(mainAnime, now);
            groupName = MediaGroup.GroupName;
        }

        // If the title appears to end with a year suffix, then remove it
        groupName = _truncateYearRegex.Replace(groupName, string.Empty);
        MediaGroup.GroupName = groupName;

        return MediaGroup;
    }

    /// <summary>
    /// Gets or creates an <see cref="MediaGroup"/> for the specified series.
    /// </summary>
    /// <param name="series">The series for which the group is to be created/retrieved (Must be initialised first).</param>
    /// <returns>The <see cref="MediaGroup"/> to use for the specified series.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="series"/> is <c>null</c>.</exception>
    public MediaGroup GetOrCreateSingleGroupForSeries(MediaSeries series)
    {
        if (series == null)
        {
            throw new ArgumentNullException(nameof(series));
        }

        MediaGroup MediaGroup;

        if (_autoGroupSeries)
        {
            var grpCalculator = AutoAnimeGroupCalculator.CreateFromServerSettings();
            var grpAnimeIds = grpCalculator.GetIdsOfAnimeInSameGroup(series.AniDB_ID ?? 0);
            // Try to find an existing MediaGroup to add the series to
            // We basically pick the first group that any of the related series belongs to already
            MediaGroup = grpAnimeIds.Where(id => id != (series.AniDB_ID ?? 0))
                .Select(id => _animeSeriesRepo.GetByAnimeID(id))
                .WhereNotNull()
                .Select(s => _animeGroupRepo.GetByID(s.MediaGroupID))
                .FirstOrDefault(s => s != null);

            var mainAnimeId = grpCalculator.GetGroupAnimeId(series.AniDB_ID ?? 0);
            // No existing group was found, so create a new one.
            if (MediaGroup == null)
            {
                // Find the main series for the group.
                var mainSeries = series.AniDB_ID == mainAnimeId ?
                    series :
                    _animeSeriesRepo.GetByAnimeID(mainAnimeId);
                MediaGroup = CreateAnimeGroup(mainSeries, mainAnimeId, DateTime.Now);
                _animeGroupRepo.Save(MediaGroup, true);
            }
            // Update the group details if we have the main series for the group.
            else if (mainAnimeId == series.AniDB_ID)
            {
                // Always update the automatic main id.
                MediaGroup.MainAniDBAnimeID = mainAnimeId;
                // Update the auto-refreshed details if the main series changed
                // and no default series is set.
                if (!MediaGroup.DefaultMediaSeriesID.HasValue)
                {
                    // Override the group name if the group is not manually named.
                    if (MediaGroup.IsManuallyNamed == 0)
                    {
                        MediaGroup.GroupName = series.Title;
                    }
                    // Override the group desc. if the group doesn't have an override.
                    if (MediaGroup.OverrideDescription == 0)
                        MediaGroup.Description = series.PreferredOverview?.Value ?? string.Empty;
                }
                MediaGroup.DateTimeUpdated = DateTime.Now;
                _animeGroupRepo.Save(MediaGroup, true);
            }
        }
        else // We're not auto grouping (e.g. we're doing group per series)
        {
            MediaGroup = new MediaGroup();
            MediaGroup.Populate(series, DateTime.Now);
            _animeGroupRepo.Save(MediaGroup, true);
        }

        return MediaGroup;
    }

    /// <summary>
    /// Gets or creates an <see cref="MediaGroup"/> for the specified series.
    /// </summary>
    /// <param name="anime">The series for which the group is to be created/retrieved (Must be initialised first).</param>
    /// <returns>The <see cref="MediaGroup"/> to use for the specified series.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="anime"/> is <c>null</c>.</exception>
    public MediaGroup GetOrCreateSingleGroupForAnime(AniDB_Anime anime)
    {
        if (anime == null)
        {
            throw new ArgumentNullException(nameof(anime));
        }

        MediaGroup MediaGroup;

        if (_autoGroupSeries)
        {
            var grpCalculator = AutoAnimeGroupCalculator.CreateFromServerSettings();
            var grpAnimeIds = grpCalculator.GetIdsOfAnimeInSameGroup(anime.AnimeID);
            // Try to find an existing MediaGroup to add the series to
            // We basically pick the first group that any of the related series belongs to already
            MediaGroup = grpAnimeIds.Where(id => id != anime.AnimeID)
                .Select(id => _animeSeriesRepo.GetByAnimeID(id))
                .WhereNotNull()
                .Select(s => _animeGroupRepo.GetByID(s.MediaGroupID))
                .FirstOrDefault(s => s != null);

            var mainAnimeId = grpCalculator.GetGroupAnimeId(anime.AnimeID);
            // No existing group was found, so create a new one.
            if (MediaGroup == null)
            {
                // Find the main series for the group.
                MediaGroup = CreateAnimeGroup(null, mainAnimeId, DateTime.Now);
                _animeGroupRepo.Save(MediaGroup, true);
            }
            // Update the group details if we have the main series for the group.
            else if (mainAnimeId == anime.AnimeID)
            {
                // Always update the automatic main id.
                MediaGroup.MainAniDBAnimeID = mainAnimeId;
                // Update the auto-refreshed details if the main series changed
                // and no default series is set.
                if (!MediaGroup.DefaultMediaSeriesID.HasValue)
                {
                    // Override the group name if the group is not manually named.
                    if (MediaGroup.IsManuallyNamed == 0)
                    {
                        MediaGroup.GroupName = anime.Title;
                    }
                    // Override the group desc. if the group doesn't have an override.
                    if (MediaGroup.OverrideDescription == 0)
                        MediaGroup.Description = anime.Description;
                }
                MediaGroup.DateTimeUpdated = DateTime.Now;
                _animeGroupRepo.Save(MediaGroup, true);
            }
        }
        else // We're not auto grouping (e.g. we're doing group per series)
        {
            MediaGroup = new MediaGroup();
            MediaGroup.Populate(anime, DateTime.Now);
            _animeGroupRepo.Save(MediaGroup, true);
        }

        return MediaGroup;
    }

    /// <summary>
    /// Re-creates all AnimeGroups based on the existing MediaSeries.
    /// </summary>
    /// <param name="session">The NHibernate session.</param>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <c>null</c>.</exception>
    private async Task RecreateAllGroups(ISessionWrapper session)
    {
        if (session == null)
        {
            throw new ArgumentNullException(nameof(session));
        }

        var paused = _queueHandler.Paused;
        var taskSource = new TaskCompletionSource();
        _systemService.AddDatabaseBlockingTask(taskSource.Task);

        try
        {
            // Pause queues
            if (!paused) await _queueHandler.Pause();
            _logger.LogInformation("Beginning re-creation of all groups");

            var MediaSeries = _animeSeriesRepo.GetAll();
            MediaGroup tempGroup = null;

            await BaseRepository.Lock(async () =>
            {
                using var trans = session.BeginTransaction();
                tempGroup = await CreateTempAnimeGroup(session);
                await ClearGroupsAndDependencies(session, tempGroup.MediaGroupID);
                await trans.CommitAsync();
            });

            var createdGroups = _autoGroupSeries
                ? (await AutoCreateGroupsWithRelatedSeries(session, MediaSeries)).AsReadOnlyCollection()
                : (await CreateGroupPerSeries(session, MediaSeries)).AsReadOnlyCollection();

            await UpdateAnimeSeriesContractsAndSave(session, MediaSeries);

            await BaseRepository.Lock(async () =>
            {
                using var trans = session.BeginTransaction();
                await session.DeleteAsync(tempGroup); // We should no longer need the temporary group we created earlier
                await trans.CommitAsync();
            });

            // We need groups and series cached for updating of MediaGroup contracts to work
            _animeGroupRepo.Populate(session, false);
            _animeSeriesRepo.Populate(session, false);

            await UpdateAnimeGroupsAndTheirContracts(createdGroups);

            // We need to update the AnimeGroups cache again now that the contracts have been saved
            // (Otherwise updating Group Filters won't get the correct results)
            _animeGroupRepo.Populate(session, false);
            _animeGroupUserRepo.Populate(session, false);

            _logger.LogInformation("Successfully completed re-creating all groups");
            taskSource.SetResult();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "An error occurred while re-creating all groups");

            try
            {
                // If an error occurs then chances are the caches are in an inconsistent state. So re-populate them
                _animeSeriesRepo.Populate();
                _animeGroupRepo.Populate();
                _animeGroupUserRepo.Populate();
            }
            catch (Exception ie)
            {
                _logger.LogWarning(ie, "Failed to re-populate caches");
            }

            taskSource.SetException(e);
            throw;
        }
        finally
        {
            // Un-pause queues (if they were previously running)
            if (!paused) await _queueHandler.Resume();
        }
    }

    public async Task RecreateAllGroups()
    {
        using var session = _databaseFactory.SessionFactory.OpenStatelessSession();
        await RecreateAllGroups(session.Wrap());
    }

    public async Task RecalculateStatsContractsForGroup(MediaGroup group)
    {
        using var sessionNotWrapped = _databaseFactory.SessionFactory.OpenSession();
        var groups = new List<MediaGroup> { group };
        var session = sessionNotWrapped.Wrap();
        var series = group.AllSeries;
        // recalculate series
        _logger.LogInformation("Recalculating Series Stats and Contracts for Group: {Name} ({ID})", group.GroupName, group.MediaGroupID);
        await BaseRepository.Lock(async () =>
        {
            using var trans = session.BeginTransaction();
            await UpdateAnimeSeriesContractsAndSave(session, series);
            await trans.CommitAsync();
        });

        // Update Cache so that group can recalculate
        series.ForEach(_animeSeriesRepo.Cache.Update);

        // Recalculate group
        _logger.LogInformation("Recalculating Group Stats and Contracts for Group: {Name} ({ID})", group.GroupName, group.MediaGroupID);
        await BaseRepository.Lock(async () =>
        {
            using var trans = session.BeginTransaction();
            await UpdateAnimeGroupsAndTheirContracts(groups);
            await trans.CommitAsync();
        });

        // update cache
        _animeGroupRepo.Cache.Update(group);
        var groupsUsers = _animeGroupUserRepo.GetByGroupID(group.MediaGroupID);
        groupsUsers.ForEach(_animeGroupUserRepo.Cache.Update);

        _logger.LogInformation("Done Recalculating Stats and Contracts for Group: {Name} ({ID})", group.GroupName, group.MediaGroupID);
    }
}
