using System.Collections.Generic;
using System.Linq;
using DaCollector.Server.Models.Internal;
using DaCollector.Server.Databases;

#nullable enable
namespace DaCollector.Server.Repositories.Direct;

public class MediaFileReviewStateRepository : BaseDirectRepository<MediaFileReviewState, int>
{
    public MediaFileReviewStateRepository(DatabaseFactory databaseFactory) : base(databaseFactory) { }

    public MediaFileReviewState? GetByVideoLocalID(int videoLocalID)
    {
        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            return session.Query<MediaFileReviewState>()
                .FirstOrDefault(a => a.VideoLocalID == videoLocalID);
        });
    }

    public IReadOnlyList<MediaFileReviewState> GetByVideoLocalIDs(IEnumerable<int> videoLocalIDs)
    {
        var ids = videoLocalIDs.Distinct().ToList();
        if (ids.Count == 0)
            return [];

        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            return session.Query<MediaFileReviewState>()
                .Where(a => ids.Contains(a.VideoLocalID))
                .ToList();
        });
    }

    public IReadOnlyList<MediaFileReviewState> GetByStatus(string status)
    {
        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            return session.Query<MediaFileReviewState>()
                .Where(a => a.Status == status)
                .OrderByDescending(a => a.UpdatedAt)
                .ToList();
        });
    }
}
