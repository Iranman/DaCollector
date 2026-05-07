using System.Collections.Generic;
using NutzCode.InMemoryIndex;
using DaCollector.Server.Databases;
using DaCollector.Server.Models.TVDB;

#nullable enable
namespace DaCollector.Server.Repositories.Cached.TVDB;

public class TVDB_EpisodeRepository : BaseCachedRepository<TVDB_Episode, int>
{
    protected override int SelectKey(TVDB_Episode entity) => entity.TVDB_EpisodeID;
    private PocoIndex<int, TVDB_Episode, int> _episodeIDs = null!;
    private PocoIndex<int, TVDB_Episode, int> _showIDs = null!;

    public override void PopulateIndexes()
    {
        _episodeIDs = Cache.CreateIndex(a => a.TvdbEpisodeID);
        _showIDs = Cache.CreateIndex(a => a.TvdbShowID);
    }

    public TVDB_Episode? GetByTvdbEpisodeID(int tvdbEpisodeId)
        => _episodeIDs.GetOne(tvdbEpisodeId);

    public IReadOnlyList<TVDB_Episode> GetByTvdbShowID(int tvdbShowId)
        => _showIDs.GetMultiple(tvdbShowId);

    public TVDB_EpisodeRepository(DatabaseFactory databaseFactory) : base(databaseFactory) { }
}
