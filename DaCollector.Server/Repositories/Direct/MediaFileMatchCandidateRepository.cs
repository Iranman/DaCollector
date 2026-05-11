using System.Collections.Generic;
using System.Linq;
using DaCollector.Server.Databases;
using DaCollector.Server.Models.Internal;

#nullable enable
namespace DaCollector.Server.Repositories.Direct;

public class MediaFileMatchCandidateRepository : BaseDirectRepository<MediaFileMatchCandidate, int>
{
    public MediaFileMatchCandidateRepository(DatabaseFactory databaseFactory) : base(databaseFactory) { }

    public IReadOnlyList<MediaFileMatchCandidate> GetByStatus(string status)
    {
        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            return session.Query<MediaFileMatchCandidate>()
                .Where(c => c.Status == status)
                .OrderByDescending(c => c.ConfidenceScore)
                .ThenBy(c => c.Title)
                .ToList();
        });
    }

    public IReadOnlyList<MediaFileMatchCandidate> GetByVideoLocalID(int videoLocalID)
    {
        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            return session.Query<MediaFileMatchCandidate>()
                .Where(c => c.VideoLocalID == videoLocalID)
                .OrderByDescending(c => c.ConfidenceScore)
                .ThenBy(c => c.Title)
                .ToList();
        });
    }

    public MediaFileMatchCandidate? GetByFileAndProvider(int videoLocalID, string provider, int providerItemID, string providerType)
    {
        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            return session.Query<MediaFileMatchCandidate>()
                .FirstOrDefault(c => c.VideoLocalID == videoLocalID
                                     && c.Provider == provider
                                     && c.ProviderItemID == providerItemID
                                     && c.ProviderType == providerType);
        });
    }
}
