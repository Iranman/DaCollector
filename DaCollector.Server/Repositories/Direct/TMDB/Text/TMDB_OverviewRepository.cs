#nullable enable
using System.Collections.Generic;
using System.Linq;
using DaCollector.Server.Databases;
using DaCollector.Server.Models.TMDB;
using DaCollector.Server.Server;

namespace DaCollector.Server.Repositories.Direct.TMDB.Text;

public class TMDB_OverviewRepository : BaseDirectRepository<TMDB_Overview, int>
{
    public IReadOnlyList<TMDB_Overview> GetByParentTypeAndID(ForeignEntityType parentType, int parentId)
    {
        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            return session
                .Query<TMDB_Overview>()
                .Where(a => a.ParentType == parentType && a.ParentID == parentId)
                .ToList();
        });
    }

    public TMDB_OverviewRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
    }
}
