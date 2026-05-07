using System.Collections.Generic;
using NutzCode.InMemoryIndex;
using DaCollector.Server.Databases;
using DaCollector.Server.Models.TVDB;

#nullable enable
namespace DaCollector.Server.Repositories.Cached.TVDB;

public class TVDB_SeasonRepository : BaseCachedRepository<TVDB_Season, int>
{
    protected override int SelectKey(TVDB_Season entity) => entity.TVDB_SeasonID;
    private PocoIndex<int, TVDB_Season, int> _seasonIDs = null!;
    private PocoIndex<int, TVDB_Season, int> _showIDs = null!;

    public override void PopulateIndexes()
    {
        _seasonIDs = Cache.CreateIndex(a => a.TvdbSeasonID);
        _showIDs = Cache.CreateIndex(a => a.TvdbShowID);
    }

    public TVDB_Season? GetByTvdbSeasonID(int tvdbSeasonId)
        => _seasonIDs.GetOne(tvdbSeasonId);

    public IReadOnlyList<TVDB_Season> GetByTvdbShowID(int tvdbShowId)
        => _showIDs.GetMultiple(tvdbShowId);

    public TVDB_SeasonRepository(DatabaseFactory databaseFactory) : base(databaseFactory) { }
}
