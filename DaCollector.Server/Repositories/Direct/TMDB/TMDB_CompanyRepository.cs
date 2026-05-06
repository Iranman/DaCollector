#nullable enable
using System.Linq;
using DaCollector.Server.Databases;
using DaCollector.Server.Models.TMDB;

namespace DaCollector.Server.Repositories.Direct.TMDB;

public class TMDB_CompanyRepository : BaseDirectRepository<TMDB_Company, int>
{
    public TMDB_Company? GetByTmdbCompanyID(int companyId)
    {
        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            return session
                .Query<TMDB_Company>()
                .Where(a => a.TmdbCompanyID == companyId)
                .Take(1)
                .SingleOrDefault();
        });
    }

    public TMDB_CompanyRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
    }
}
