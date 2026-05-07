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
public class GetTvdbShowJob : BaseJob
{
    private readonly TvdbMetadataService _tvdbService;

    public virtual int TvdbShowID { get; set; }

    public override string TypeName => "Get TVDB Show";
    public override string Title => $"Getting TVDB Show {TvdbShowID}";
    public override Dictionary<string, object> Details => new() { { "TvdbShowID", TvdbShowID } };

    public override async Task Process()
    {
        await _tvdbService.UpdateShow(TvdbShowID).ConfigureAwait(false);
    }

    public GetTvdbShowJob(TvdbMetadataService tvdbService)
    {
        _tvdbService = tvdbService;
    }

    protected GetTvdbShowJob() { }
}
