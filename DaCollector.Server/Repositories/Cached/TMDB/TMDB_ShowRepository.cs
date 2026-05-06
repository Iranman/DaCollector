using NutzCode.InMemoryIndex;
using DaCollector.Server.Databases;
using DaCollector.Server.Models.TMDB;

#nullable enable
namespace DaCollector.Server.Repositories.Cached.TMDB;

public class TMDB_ShowRepository : BaseCachedRepository<TMDB_Show, int>
{
    protected override int SelectKey(TMDB_Show entity) => entity.Id;
    private PocoIndex<int, TMDB_Show, int> _showIDs = null!;

    public override void PopulateIndexes()
    {
        _showIDs = Cache.CreateIndex(a => a.TmdbShowID);
    }

    public TMDB_Show? GetByTmdbShowID(int tmdbShowId)
    {
        return _showIDs.GetOne(tmdbShowId);
    }

    public TMDB_ShowRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
    }
}
