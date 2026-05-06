#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using DaCollector.Server.Databases;
using DaCollector.Server.Models.TMDB;
using DaCollector.Server.Server;

namespace DaCollector.Server.Repositories.Direct.TMDB;

public class TMDB_Company_EntityRepository : BaseDirectRepository<TMDB_Company_Entity, int>
{
    public IReadOnlyList<TMDB_Company_Entity> GetByTmdbCompanyID(int companyId)
    {
        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            return session
                .Query<TMDB_Company_Entity>()
                .Where(a => a.TmdbCompanyID == companyId)
                .OrderBy(xref => xref.ReleasedAt ?? DateOnly.MaxValue)
                .ToList();
        });
    }

    public IReadOnlyList<TMDB_Company_Entity> GetByTmdbEntityTypeAndCompanyID(ForeignEntityType entityType, int companyId)
    {
        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            return session
                .Query<TMDB_Company_Entity>()
                .Where(a => a.TmdbCompanyID == companyId && a.TmdbEntityType == entityType)
                .OrderBy(xref => xref.ReleasedAt ?? DateOnly.MaxValue)
                .ToList();
        });
    }

    public IReadOnlyList<TMDB_Company_Entity> GetByTmdbEntityTypeAndID(ForeignEntityType entityType, int entityId)
    {
        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            return session
                .Query<TMDB_Company_Entity>()
                .Where(a => a.TmdbEntityType == entityType && a.TmdbEntityID == entityId)
                .OrderBy(xref => xref.Ordering)
                .ToList();
        });
    }

    public TMDB_Company_EntityRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
    }
}
