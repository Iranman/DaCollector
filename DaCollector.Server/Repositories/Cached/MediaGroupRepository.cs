using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NutzCode.InMemoryIndex;
using DaCollector.Server.Databases;
using DaCollector.Server.Models.DaCollector;
using DaCollector.Server.Repositories.NHibernate;

#nullable enable
namespace DaCollector.Server.Repositories.Cached;

public class MediaGroupRepository : BaseCachedRepository<MediaGroup, int>
{
    private readonly ILogger<MediaGroupRepository> _logger;

    private PocoIndex<int, MediaGroup, int>? _parentIDs;

    private readonly ChangeTracker<int> _changes = new();

    public MediaGroupRepository(ILogger<MediaGroupRepository> logger, DatabaseFactory databaseFactory) : base(databaseFactory)
    {
        _logger = logger;
        BeginDeleteCallback = cr =>
        {
            RepoFactory.MediaGroup_User.Delete(RepoFactory.MediaGroup_User.GetByGroupID(cr.MediaGroupID));
        };
        EndDeleteCallback = cr =>
        {
            if (cr.MediaGroupParentID.HasValue && cr.MediaGroupParentID.Value > 0)
            {
                _logger.LogTrace("Updating group stats by group from MediaGroupRepository.Delete: {Count}", cr.MediaGroupParentID.Value);
                var parentGroup = GetByID(cr.MediaGroupParentID.Value);
                if (parentGroup != null)
                {
                    Save(parentGroup, true);
                }
            }
        };
    }

    protected override int SelectKey(MediaGroup entity)
        => entity.MediaGroupID;

    public override void PopulateIndexes()
    {
        _changes.AddOrUpdateRange(Cache.Keys);
        _parentIDs = Cache.CreateIndex(a => a.MediaGroupParentID ?? 0);
    }

    public override void Save(MediaGroup obj)
        => Save(obj, true);

    public void Save(MediaGroup group, bool recursive)
    {
        using var session = _databaseFactory.SessionFactory.OpenSession();
        Lock(session, s =>
        {
            //We are creating one, and we need the MediaGroupID before Update the contracts
            if (group.MediaGroupID == 0)
            {
                using var transaction = s.BeginTransaction();
                s.SaveOrUpdate(group);
                transaction.Commit();
            }
        });

        UpdateCache(group);
        Lock(session, s =>
        {
            using var transaction = s.BeginTransaction();
            SaveWithOpenTransaction(s, group);
            transaction.Commit();
        });

        _changes.AddOrUpdate(group.MediaGroupID);

        if (group.MediaGroupParentID.HasValue && recursive)
        {
            var parentGroup = GetByID(group.MediaGroupParentID.Value);
            // This will avoid the recursive error that would be possible, it won't update it, but that would be
            // the least of the issues
            if (parentGroup != null && parentGroup.MediaGroupParentID == group.MediaGroupID)
            {
                Save(parentGroup, true);
            }
        }
    }

    public async Task InsertBatch(ISessionWrapper session, IReadOnlyCollection<MediaGroup> groups)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(groups);

        using var trans = session.BeginTransaction();
        foreach (var group in groups)
        {
            await session.InsertAsync(group);
            UpdateCache(group);
        }

        await trans.CommitAsync();

        _changes.AddOrUpdateRange(groups.Select(g => g.MediaGroupID));
    }

    public async Task UpdateBatch(ISessionWrapper session, IReadOnlyCollection<MediaGroup> groups)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(groups);

        using var trans = session.BeginTransaction();
        foreach (var group in groups)
        {
            await session.UpdateAsync(group);
            UpdateCache(group);
        }

        await trans.CommitAsync();

        _changes.AddOrUpdateRange(groups.Select(g => g.MediaGroupID));
    }

    /// <summary>
    /// Deletes all MediaGroup records.
    /// </summary>
    /// <remarks>
    /// This method also makes sure that the cache is cleared.
    /// </remarks>
    /// <param name="session">The NHibernate session.</param>
    /// <param name="excludeGroupId">The ID of the MediaGroup to exclude from deletion.</param>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <c>null</c>.</exception>
    public async Task DeleteAll(ISessionWrapper session, int? excludeGroupId = null)
    {
        ArgumentNullException.ThrowIfNull(session);

        // First, get all of the current groups so that we can inform the change tracker that they have been removed later
        var allGroups = GetAll();

        await Lock(async () =>
        {
            // Then, actually delete the AnimeGroups
            if (excludeGroupId != null)
            {
                await session.CreateSQLQuery("DELETE FROM MediaGroup WHERE MediaGroupID <> :excludeId")
                    .SetInt32("excludeId", excludeGroupId.Value)
                    .ExecuteUpdateAsync();
            }
            else
            {
                await session.CreateSQLQuery("DELETE FROM MediaGroup WHERE MediaGroupID > 0")
                    .ExecuteUpdateAsync();
            }
        });

        if (excludeGroupId != null)
        {
            _changes.RemoveRange(allGroups.Select(g => g.MediaGroupID)
                .Where(id => id != excludeGroupId.Value));
        }
        else
        {
            _changes.RemoveRange(allGroups.Select(g => g.MediaGroupID));
        }

        // Finally, we need to clear the cache so that it is in sync with the database
        ClearCache();

        // If we're excluding a group from deletion, and it was in the cache originally, then re-add it back in
        if (excludeGroupId != null)
        {
            var excludedGroup = allGroups.FirstOrDefault(g => g.MediaGroupID == excludeGroupId.Value);

            if (excludedGroup != null)
            {
                UpdateCache(excludedGroup);
            }
        }
    }

    public List<MediaGroup> GetByParentID(int parentID)
        => ReadLock(() => _parentIDs!.GetMultiple(parentID));

    public List<MediaGroup> GetAllTopLevelGroups()
        => GetByParentID(0);

    public ChangeTracker<int> GetChangeTracker()
        => _changes;
}
