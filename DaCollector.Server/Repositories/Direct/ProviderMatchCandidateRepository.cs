using System.Collections.Generic;
using System.Linq;
using DaCollector.Server.Databases;
using DaCollector.Server.Models.Internal;

#nullable enable
namespace DaCollector.Server.Repositories.Direct;

public class ProviderMatchCandidateRepository : BaseDirectRepository<ProviderMatchCandidate, int>
{
    public ProviderMatchCandidateRepository(DatabaseFactory databaseFactory) : base(databaseFactory) { }

    public IReadOnlyList<ProviderMatchCandidate> GetByStatus(string status)
    {
        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            return session.Query<ProviderMatchCandidate>()
                .Where(c => c.Status == status)
                .OrderByDescending(c => c.ConfidenceScore)
                .ToList();
        });
    }

    public IReadOnlyList<ProviderMatchCandidate> GetByMediaSeriesID(int mediaSeriesID)
    {
        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            return session.Query<ProviderMatchCandidate>()
                .Where(c => c.MediaSeriesID == mediaSeriesID)
                .OrderByDescending(c => c.ConfidenceScore)
                .ToList();
        });
    }

    /// <summary>
    /// Returns the single existing candidate for the series/provider/item combination, or null if none exists.
    /// The unique constraint on these four columns guarantees at most one row.
    /// </summary>
    public ProviderMatchCandidate? GetBySeriesAndProvider(int mediaSeriesID, string provider, int providerItemID, string providerType)
    {
        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            return session.Query<ProviderMatchCandidate>()
                .FirstOrDefault(c => c.MediaSeriesID == mediaSeriesID
                                     && c.Provider == provider
                                     && c.ProviderItemID == providerItemID
                                     && c.ProviderType == providerType);
        });
    }
}
