using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using DaCollector.Server.Databases;
using DaCollector.Server.Models.DaCollector;

#nullable enable
namespace DaCollector.Server.Repositories.Cached;

public class MediaEpisode_UserRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<MediaEpisode_User, int>(databaseFactory)
{
    private PocoIndex<int, MediaEpisode_User, int>? _userIDs;

    private PocoIndex<int, MediaEpisode_User, int>? _episodeIDs;

    private PocoIndex<int, MediaEpisode_User, (int UserID, int EpisodeID)>? _userEpisodeIDs;

    private PocoIndex<int, MediaEpisode_User, (int UserID, int SeriesID)>? _userSeriesIDs;

    protected override int SelectKey(MediaEpisode_User entity)
        => entity.MediaEpisode_UserID;

    public override void PopulateIndexes()
    {
        _userIDs = Cache.CreateIndex(a => a.JMMUserID);
        _episodeIDs = Cache.CreateIndex(a => a.MediaEpisodeID);
        _userEpisodeIDs = Cache.CreateIndex(a => (a.JMMUserID, a.MediaEpisodeID));
        _userSeriesIDs = Cache.CreateIndex(a => (a.JMMUserID, a.MediaSeriesID));
    }

    public override void RegenerateDb()
    {
        var current = 0;
        var records = Cache.Values.Where(a => a.MediaEpisode_UserID == 0).ToList();
        var total = records.Count;
        SystemService.StartupMessage = $"Database - Validating - {nameof(MediaEpisode_User)} Database Regeneration...";
        if (total is 0)
            return;

        foreach (var record in records)
        {
            Save(record);
            current++;
            if (current % 10 == 0)
                SystemService.StartupMessage =
                    $"Database - Validating - {nameof(MediaEpisode_User)} Database Regeneration - {current}/{total}...";
        }

        SystemService.StartupMessage =
            $"Database - Validating - {nameof(MediaEpisode_User)} Database Regeneration - {total}/{total}...";
    }

    public MediaEpisode_User? GetByUserAndEpisodeID(int userID, int episodeID)
        => ReadLock(() => _userEpisodeIDs!.GetOne((userID, episodeID)));

    public IReadOnlyList<MediaEpisode_User> GetByUserID(int userid)
        => ReadLock(() => _userIDs!.GetMultiple(userid));

    public IReadOnlyList<MediaEpisode_User> GetMostRecentlyWatched(int userid, int limit = 100)
        => GetByUserID(userid).Where(a => a.WatchedCount > 0)
            .OrderByDescending(a => a.WatchedDate)
            .Take(limit)
            .ToList();

    public MediaEpisode_User? GetLastWatchedEpisodeForSeries(int seriesID, int userID)
        => GetByUserIDAndSeriesID(userID, seriesID)
            .Where(a => a.WatchedCount > 0)
            .OrderByDescending(a => a.WatchedDate)
            .FirstOrDefault();

    public IReadOnlyList<MediaEpisode_User> GetByEpisodeID(int episodeID)
        => ReadLock(() => _episodeIDs!.GetMultiple(episodeID));

    public IReadOnlyList<MediaEpisode_User> GetByUserIDAndSeriesID(int userID, int seriesID)
        => ReadLock(() => _userSeriesIDs!.GetMultiple((userID, seriesID)));
}
