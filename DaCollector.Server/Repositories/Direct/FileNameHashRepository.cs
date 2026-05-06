using System.Collections.Generic;
using System.Linq;
using DaCollector.Server.Databases;
using DaCollector.Server.Models.DaCollector;

namespace DaCollector.Server.Repositories.Direct;

public class FileNameHashRepository : BaseDirectRepository<FileNameHash, int>
{
    public List<FileNameHash> GetByHash(string hash)
    {
        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            return session
                .Query<FileNameHash>()
                .Where(a => a.Hash == hash)
                .ToList();
        });
    }

    public List<FileNameHash> GetByFileNameAndSize(string filename, long filesize)
    {
        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            return session
                .Query<FileNameHash>()
                .Where(a => a.FileName == filename && a.FileSize == filesize)
                .ToList();
        });
    }

    public FileNameHashRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
    }
}
