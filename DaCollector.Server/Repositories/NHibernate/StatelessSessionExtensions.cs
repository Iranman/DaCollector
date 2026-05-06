using System.Diagnostics;
using NHibernate;

namespace DaCollector.Server.Repositories.NHibernate;

internal static class StatelessSessionExtensions
{
    [DebuggerStepThrough]
    public static ISessionWrapper Wrap(this IStatelessSession session)
    {
        return new StatelessSessionWrapper(session);
    }
}
