using System.Collections.Generic;
using System.Threading.Tasks;
using DaCollector.Server.Providers.TVDB;
using DaCollector.Server.Scheduling.Acquisition.Attributes;
using DaCollector.Server.Scheduling.Attributes;
using DaCollector.Server.Scheduling.Concurrency;

#nullable enable
#pragma warning disable CS8618
namespace DaCollector.Server.Scheduling.Jobs.TVDB;

[DatabaseRequired]
[NetworkRequired]
[LimitConcurrency(4, 8)]
[JobKeyGroup(JobKeyGroup.TVDB)]
public class GetTvdbMovieJob : BaseJob
{
    private readonly TvdbMetadataService _tvdbService;

    public virtual int TvdbMovieID { get; set; }

    public override string TypeName => "Get TVDB Movie";
    public override string Title => $"Getting TVDB Movie {TvdbMovieID}";
    public override Dictionary<string, object> Details => new() { { "TvdbMovieID", TvdbMovieID } };

    public override async Task Process()
    {
        await _tvdbService.UpdateMovie(TvdbMovieID).ConfigureAwait(false);
    }

    public GetTvdbMovieJob(TvdbMetadataService tvdbService)
    {
        _tvdbService = tvdbService;
    }

    protected GetTvdbMovieJob() { }
}
