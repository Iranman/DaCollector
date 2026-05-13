using System.Collections.Generic;
using NutzCode.InMemoryIndex;
using DaCollector.Server.Databases;
using DaCollector.Server.Models.CrossReference;

#nullable enable
namespace DaCollector.Server.Repositories.Cached;

public class CrossRef_File_TmdbEpisodeRepository : BaseCachedRepository<CrossRef_File_TmdbEpisode, int>
{
    private PocoIndex<int, CrossRef_File_TmdbEpisode, int>? _videoLocalIDs;

    private PocoIndex<int, CrossRef_File_TmdbEpisode, int>? _tmdbEpisodeIDs;

    public CrossRef_File_TmdbEpisodeRepository(DatabaseFactory databaseFactory) : base(databaseFactory) { }

    protected override int SelectKey(CrossRef_File_TmdbEpisode entity)
        => entity.CrossRef_File_TmdbEpisodeID;

    public override void PopulateIndexes()
    {
        _videoLocalIDs = Cache.CreateIndex(a => a.VideoLocalID);
        _tmdbEpisodeIDs = Cache.CreateIndex(a => a.TmdbEpisodeID);
    }

    public IReadOnlyList<CrossRef_File_TmdbEpisode> GetByVideoLocalID(int videoLocalID)
        => ReadLock(() => _videoLocalIDs!.GetMultiple(videoLocalID));

    public IReadOnlyList<CrossRef_File_TmdbEpisode> GetByTmdbEpisodeID(int tmdbEpisodeID)
        => ReadLock(() => _tmdbEpisodeIDs!.GetMultiple(tmdbEpisodeID));
}
