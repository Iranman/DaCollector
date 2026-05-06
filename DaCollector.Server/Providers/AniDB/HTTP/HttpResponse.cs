using System.Net;
using DaCollector.Server.Providers.AniDB.Interfaces;

namespace DaCollector.Server.Providers.AniDB.HTTP;

public class HttpResponse<T> : IResponse<T> where T : class
{
    public HttpStatusCode Code { get; set; }

    public T Response { get; set; }
}
