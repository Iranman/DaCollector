using System.Threading.Tasks;
using Quartz;
using DaCollector.Abstractions.Video.Services;
using DaCollector.Server.Scheduling.Acquisition.Attributes;
using DaCollector.Server.Scheduling.Attributes;
using DaCollector.Server.Services;

namespace DaCollector.Server.Scheduling.Jobs.Actions;

[DatabaseRequired]
[JobKeyMember("Import")]
[JobKeyGroup(JobKeyGroup.Legacy)]
[DisallowConcurrentExecution]
public class ImportJob : BaseJob
{
    private readonly IVideoService _videoService;

    private readonly ActionService _service;
    public override string TypeName => "Run Import";
    public override string Title => "Running Import";

    public override async Task Process()
    {
        await _service.RunImport_IntegrityCheck();

        // managed folder
        await _videoService.ScheduleScanForManagedFolders();

        // TMDB association checks
        await _service.RunImport_ScanTMDB();

        // TMDB Purge people
        await _service.RunImport_PurgeUnlinkedTmdbPeople();

        // TMDB Purge networks
        await _service.RunImport_PurgeUnlinkedTmdbShowNetworks();

        // Check for missing images
        _service.RunImport_GetImages();

        // Check for previously ignored files
        _service.CheckForPreviouslyIgnored();

        await _service.ScheduleMissingAnidbAnimeForFiles();
    }

    public ImportJob(IVideoService videoService, ActionService service)
    {
        _videoService = videoService;
        _service = service;
    }

    protected ImportJob() { }
}
