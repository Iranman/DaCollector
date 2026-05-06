using System.Threading;
using DaCollector.Server.Repositories.NHibernate;

namespace DaCollector.Server.Repositories;

public interface ICachedRepository
{
    // Listed in order of call
    void Populate(bool displayName = true, CancellationToken cancellationToken = default);
    void Populate(ISessionWrapper session, bool displayName = true, CancellationToken cancellationToken = default);
    void PopulateIndexes();
    void RegenerateDb();
    void PostProcess();
}
