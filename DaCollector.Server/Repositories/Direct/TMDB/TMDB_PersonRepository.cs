#nullable enable
using System.Linq;
using DaCollector.Server.Databases;
using DaCollector.Server.Models.TMDB;

namespace DaCollector.Server.Repositories.Direct.TMDB;

public class TMDB_PersonRepository : BaseDirectRepository<TMDB_Person, int>
{
    public TMDB_Person? GetByTmdbPersonID(int creditId)
    {
        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            return session
                .Query<TMDB_Person>()
                .Where(a => a.TmdbPersonID == creditId)
                .Take(1)
                .SingleOrDefault();
        });
    }

    public TMDB_PersonRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
    }
}
