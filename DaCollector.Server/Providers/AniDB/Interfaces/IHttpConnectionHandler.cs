using System.Threading.Tasks;
using DaCollector.Server.Providers.AniDB.HTTP;

namespace DaCollector.Server.Providers.AniDB.Interfaces;

public interface IHttpConnectionHandler : IConnectionHandler
{
    Task<HttpResponse<string>> GetHttp(string url, bool force = false);
}
