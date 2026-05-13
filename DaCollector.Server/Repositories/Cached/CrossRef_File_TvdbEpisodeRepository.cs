using System.Collections.Generic;
using NutzCode.InMemoryIndex;
using DaCollector.Server.Databases;
using DaCollector.Server.Models.CrossReference;

#nullable enable
namespace DaCollector.Server.Repositories.Cached;

public class CrossRef_File_TvdbEpisodeRepository : BaseCachedRepository<CrossRef_File_TvdbEpisode, int>
{
    private PocoIndex<int, CrossRef_File_TvdbEpisode, int>? _videoLocalIDs;

    private PocoIndex<int, CrossRef_File_TvdbEpisode, int>? _tvdbEpisodeIDs;

    public CrossRef_File_TvdbEpisodeRepository(DatabaseFactory databaseFactory) : base(databaseFactory) { }

    protected override int SelectKey(CrossRef_File_TvdbEpisode entity)
        => entity.CrossRef_File_TvdbEpisodeID;

    public override void PopulateIndexes()
    {
        _videoLocalIDs = Cache.CreateIndex(a => a.VideoLocalID);
        _tvdbEpisodeIDs = Cache.CreateIndex(a => a.TvdbEpisodeID);
    }

    public IReadOnlyList<CrossRef_File_TvdbEpisode> GetByVideoLocalID(int videoLocalID)
        => ReadLock(() => _videoLocalIDs!.GetMultiple(videoLocalID));

    public IReadOnlyList<CrossRef_File_TvdbEpisode> GetByTvdbEpisodeID(int tvdbEpisodeID)
        => ReadLock(() => _tvdbEpisodeIDs!.GetMultiple(tvdbEpisodeID));
}
