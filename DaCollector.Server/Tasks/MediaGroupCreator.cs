#nullable enable
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
using DaCollector.Server.Models.DaCollector;
using DaCollector.Server.Models.TMDB;
using DaCollector.Server.Repositories;
using DaCollector.Server.Repositories.Cached;
using DaCollector.Server.Repositories.Direct.TMDB.Optional;
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
    private readonly MediaSeriesRepository _animeSeriesRepo;
    private readonly MediaGroupRepository _animeGroupRepo;
    private readonly MediaGroup_UserRepository _animeGroupUserRepo;
    private readonly TMDB_Collection_MovieRepository _collectionMovieRepo;
    private readonly TMDB_CollectionRepository _collectionRepo;
    private readonly bool _autoGroupSeries;
    private readonly DatabaseFactory _databaseFactory;

    public MediaGroupCreator(
        SystemService systemService,
        ISettingsProvider settingsProvider,
        QueueHandler queueHandler,
        ILogger<MediaGroupCreator> logger,
        DatabaseFactory databaseFactory,
        MediaSeriesRepository animeSeriesRepo,
        MediaGroupRepository animeGroupRepo,
        MediaGroup_UserRepository animeGroupUserRepo,
        MediaGroupService groupService,
        TMDB_Collection_MovieRepository collectionMovieRepo,
        TMDB_CollectionRepository collectionRepo
    )
    {
        _systemService = systemService;
        _queueHandler = queueHandler;
        _logger = logger;
        _databaseFactory = databaseFactory;
        _animeSeriesRepo = animeSeriesRepo;
        _animeGroupRepo = animeGroupRepo;
        _animeGroupUserRepo = animeGroupUserRepo;
        _groupService = groupService;
        _collectionMovieRepo = collectionMovieRepo;
        _collectionRepo = collectionRepo;
        _autoGroupSeries = settingsProvider.GetSettings().AutoGroupSeries;
    }

    private (int key, TMDB_Collection? collection) GetCollectionKey(MediaSeries series)
    {
        if (series.TMDB_MovieID.HasValue)
        {
            var xref = _collectionMovieRepo.GetByTmdbMovieID(series.TMDB_MovieID.Value);
            if (xref != null)
            {
                var collection = _collectionRepo.GetByTmdbCollectionID(xref.TmdbCollectionID);
                if (collection != null)
                    return (xref.TmdbCollectionID, collection);
            }
        }
        // Negative series ID ensures no collision with positive TMDB collection IDs
        return (-series.MediaSeriesID, null);
    }

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

        await session.InsertAsync(tempGroup);
        lock (_animeGroupRepo.Cache)
        {
            _animeGroupRepo.Cache.Update(tempGroup);
        }

        return tempGroup;
    }

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
        var animeGroupUsers = allGroupUsers.SelectMany(groupUsers => groupUsers).ToList();
        _animeGroupUserRepo.Save(animeGroupUsers);
        _logger.LogInformation("MediaGroup statistics have been saved");

        return Task.CompletedTask;
    }

    private async Task<IEnumerable<MediaGroup>> CreateGroupPerSeries(ISessionWrapper session, IReadOnlyList<MediaSeries> seriesList)
    {
        _logger.LogInformation("Generating AnimeGroups for {Count} MediaSeries", seriesList.Count);

        var now = DateTime.Now;
        var newGroupsToSeries = new Tuple<MediaGroup, MediaSeries>[seriesList.Count];

        for (var grp = 0; grp < seriesList.Count; grp++)
        {
            var group = new MediaGroup();
            var series = seriesList[grp];
            group.Populate(series, now);
            newGroupsToSeries[grp] = new Tuple<MediaGroup, MediaSeries>(group, series);
        }

        await BaseRepository.Lock(async () => await _animeGroupRepo.InsertBatch(session, newGroupsToSeries.Select(gts => gts.Item1).AsReadOnlyCollection()));

        foreach (var groupAndSeries in newGroupsToSeries)
        {
            groupAndSeries.Item2.MediaGroupID = groupAndSeries.Item1.MediaGroupID;
        }

        _logger.LogInformation("Generated {Count} AnimeGroups", newGroupsToSeries.Length);
        return newGroupsToSeries.Select(gts => gts.Item1);
    }

    private async Task<IEnumerable<MediaGroup>> AutoCreateGroupsByCollection(ISessionWrapper session, IReadOnlyCollection<MediaSeries> seriesList)
    {
        _logger.LogInformation("Auto-generating MediaGroups for {Count} MediaSeries based on TMDB collections", seriesList.Count);

        var now = DateTime.Now;
        var seriesByGroup = seriesList.ToLookup(s => GetCollectionKey(s).key);
        var newGroupsToSeries = new List<(MediaGroup group, IReadOnlyCollection<MediaSeries> series)>();

        foreach (var grouping in seriesByGroup)
        {
            var (_, collection) = GetCollectionKey(grouping.First());
            var group = new MediaGroup { DateTimeCreated = now, DateTimeUpdated = now };

            if (collection != null)
            {
                group.GroupName = _truncateYearRegex.Replace(
                    collection.GetPreferredTitle()?.Value ?? collection.EnglishTitle, string.Empty);
                group.Description = collection.GetPreferredOverview()?.Value ?? collection.EnglishOverview;
                group.TmdbCollectionID = collection.TmdbCollectionID;
            }
            else
            {
                group.Populate(grouping.First(), now);
            }

            newGroupsToSeries.Add((group, grouping.AsReadOnlyCollection()));
        }

        await BaseRepository.Lock(async () =>
            await _animeGroupRepo.InsertBatch(session,
                newGroupsToSeries.Select(x => x.group).AsReadOnlyCollection()));

        foreach (var (group, series) in newGroupsToSeries)
            foreach (var s in series)
                s.MediaGroupID = group.MediaGroupID;

        _logger.LogInformation("Generated {Count} MediaGroups", newGroupsToSeries.Count);
        return newGroupsToSeries.Select(x => x.group);
    }

    public MediaGroup GetOrCreateSingleGroupForSeries(MediaSeries series)
    {
        if (series == null)
            throw new ArgumentNullException(nameof(series));

        if (!_autoGroupSeries)
        {
            var group = new MediaGroup();
            group.Populate(series, DateTime.Now);
            _animeGroupRepo.Save(group, true);
            return group;
        }

        var (_, collection) = GetCollectionKey(series);

        if (collection != null)
        {
            var existing = _animeSeriesRepo.GetAll()
                .Select(s => _animeGroupRepo.GetByID(s.MediaGroupID))
                .FirstOrDefault(g => g?.TmdbCollectionID == collection.TmdbCollectionID);

            if (existing != null)
            {
                if (existing.IsManuallyNamed == 0 && !existing.DefaultMediaSeriesID.HasValue)
                {
                    existing.GroupName = collection.GetPreferredTitle()?.Value ?? collection.EnglishTitle;
                    existing.DateTimeUpdated = DateTime.Now;
                    _animeGroupRepo.Save(existing, true);
                }
                return existing;
            }

            var newGroup = new MediaGroup
            {
                GroupName = _truncateYearRegex.Replace(
                    collection.GetPreferredTitle()?.Value ?? collection.EnglishTitle, string.Empty),
                Description = collection.GetPreferredOverview()?.Value ?? collection.EnglishOverview,
                TmdbCollectionID = collection.TmdbCollectionID,
                DateTimeCreated = DateTime.Now,
                DateTimeUpdated = DateTime.Now,
            };
            _animeGroupRepo.Save(newGroup, true);
            return newGroup;
        }

        var standalone = new MediaGroup();
        standalone.Populate(series, DateTime.Now);
        _animeGroupRepo.Save(standalone, true);
        return standalone;
    }

    private async Task RecreateAllGroups(ISessionWrapper session)
    {
        if (session == null)
            throw new ArgumentNullException(nameof(session));

        var paused = _queueHandler.Paused;
        var taskSource = new TaskCompletionSource();
        _systemService.AddDatabaseBlockingTask(taskSource.Task);

        try
        {
            if (!paused) await _queueHandler.Pause();
            _logger.LogInformation("Beginning re-creation of all groups");

            var mediaSeries = _animeSeriesRepo.GetAll();
            MediaGroup? tempGroup = null;

            await BaseRepository.Lock(async () =>
            {
                using var trans = session.BeginTransaction();
                tempGroup = await CreateTempAnimeGroup(session);
                await ClearGroupsAndDependencies(session, tempGroup.MediaGroupID);
                await trans.CommitAsync();
            });

            var createdGroups = _autoGroupSeries
                ? (await AutoCreateGroupsByCollection(session, mediaSeries)).AsReadOnlyCollection()
                : (await CreateGroupPerSeries(session, mediaSeries)).AsReadOnlyCollection();

            await UpdateAnimeSeriesContractsAndSave(session, mediaSeries);

            await BaseRepository.Lock(async () =>
            {
                using var trans = session.BeginTransaction();
                await session.DeleteAsync(tempGroup);
                await trans.CommitAsync();
            });

            _animeGroupRepo.Populate(session, false);
            _animeSeriesRepo.Populate(session, false);

            await UpdateAnimeGroupsAndTheirContracts(createdGroups);

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

        _logger.LogInformation("Recalculating Series Stats and Contracts for Group: {Name} ({ID})", group.GroupName, group.MediaGroupID);
        await BaseRepository.Lock(async () =>
        {
            using var trans = session.BeginTransaction();
            await UpdateAnimeSeriesContractsAndSave(session, series);
            await trans.CommitAsync();
        });

        series.ForEach(_animeSeriesRepo.Cache.Update);

        _logger.LogInformation("Recalculating Group Stats and Contracts for Group: {Name} ({ID})", group.GroupName, group.MediaGroupID);
        await BaseRepository.Lock(async () =>
        {
            using var trans = session.BeginTransaction();
            await UpdateAnimeGroupsAndTheirContracts(groups);
            await trans.CommitAsync();
        });

        _animeGroupRepo.Cache.Update(group);
        var groupsUsers = _animeGroupUserRepo.GetByGroupID(group.MediaGroupID);
        groupsUsers.ForEach(_animeGroupUserRepo.Cache.Update);

        _logger.LogInformation("Done Recalculating Stats and Contracts for Group: {Name} ({ID})", group.GroupName, group.MediaGroupID);
    }
}
