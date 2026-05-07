using NutzCode.InMemoryIndex;
using DaCollector.Server.Databases;
using DaCollector.Server.Models.TVDB;

#nullable enable
namespace DaCollector.Server.Repositories.Cached.TVDB;

public class TVDB_MovieRepository : BaseCachedRepository<TVDB_Movie, int>
{
    protected override int SelectKey(TVDB_Movie entity) => entity.TVDB_MovieID;
    private PocoIndex<int, TVDB_Movie, int> _movieIDs = null!;

    public override void PopulateIndexes()
    {
        _movieIDs = Cache.CreateIndex(a => a.TvdbMovieID);
    }

    public TVDB_Movie? GetByTvdbMovieID(int tvdbMovieId)
        => _movieIDs.GetOne(tvdbMovieId);

    public TVDB_MovieRepository(DatabaseFactory databaseFactory) : base(databaseFactory) { }
}
