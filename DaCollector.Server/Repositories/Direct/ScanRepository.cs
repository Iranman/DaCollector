using DaCollector.Server.Databases;
using DaCollector.Server.Models.Legacy;

namespace DaCollector.Server.Repositories.Direct;

public class ScanRepository : BaseDirectRepository<Scan, int>
{
    public ScanRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
    }
}
