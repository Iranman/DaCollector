using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using DaCollector.Server.Databases;
using DaCollector.Server.Models.DaCollector;

#nullable enable
namespace DaCollector.Server.Repositories.Cached;

public class MediaSeries_UserRepository : BaseCachedRepository<MediaSeries_User, int>
{
    private PocoIndex<int, MediaSeries_User, int>? _userIDs;

    private PocoIndex<int, MediaSeries_User, int>? _seriesIDs;

    private PocoIndex<int, MediaSeries_User, (int UserID, int SeriesID)>? _userSeriesIDs;

    private readonly Dictionary<int, ChangeTracker<int>> _changes = [];

    public MediaSeries_UserRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
        EndDeleteCallback = cr =>
        {
            _changes.TryAdd(cr.JMMUserID, new ChangeTracker<int>());

            _changes[cr.JMMUserID].Remove(cr.MediaSeriesID);
        };
    }

    protected override int SelectKey(MediaSeries_User entity)
        => entity.MediaSeries_UserID;

    public override void PopulateIndexes()
    {
        _userIDs = Cache.CreateIndex(a => a.JMMUserID);
        _seriesIDs = Cache.CreateIndex(a => a.MediaSeriesID);
        _userSeriesIDs = Cache.CreateIndex(a => (a.JMMUserID, a.MediaSeriesID));
    }

    public override void Save(MediaSeries_User obj)
    {
        base.Save(obj);
        _changes.TryAdd(obj.JMMUserID, new());
        _changes[obj.JMMUserID].AddOrUpdate(obj.MediaSeriesID);
    }

    public MediaSeries_User? GetByUserAndSeriesID(int userID, int seriesID)
        => ReadLock(() => _userSeriesIDs!.GetOne((userID, seriesID)));

    public List<MediaSeries_User> GetByUserID(int userID)
        => ReadLock(() => _userIDs!.GetMultiple(userID));

    public List<MediaSeries_User> GetBySeriesID(int seriesID)
        => ReadLock(() => _seriesIDs!.GetMultiple(seriesID));

    public List<MediaSeries_User> GetMostRecentlyWatched(int userID)
        => GetByUserID(userID)
            .Where(a => a.UnwatchedEpisodeCount > 0)
            .OrderByDescending(a => a.WatchedDate)
            .ToList();

    public ChangeTracker<int> GetChangeTracker(int userID)
        => _changes.TryGetValue(userID, out var change) ? change : new ChangeTracker<int>();
}
