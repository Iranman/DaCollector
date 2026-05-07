using NutzCode.InMemoryIndex;
using DaCollector.Server.Databases;
using DaCollector.Server.Models.TVDB;

#nullable enable
namespace DaCollector.Server.Repositories.Cached.TVDB;

public class TVDB_ShowRepository : BaseCachedRepository<TVDB_Show, int>
{
    protected override int SelectKey(TVDB_Show entity) => entity.TVDB_ShowID;
    private PocoIndex<int, TVDB_Show, int> _showIDs = null!;

    public override void PopulateIndexes()
    {
        _showIDs = Cache.CreateIndex(a => a.TvdbShowID);
    }

    public TVDB_Show? GetByTvdbShowID(int tvdbShowId)
        => _showIDs.GetOne(tvdbShowId);

    public TVDB_ShowRepository(DatabaseFactory databaseFactory) : base(databaseFactory) { }
}
