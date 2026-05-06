using System.Threading.Tasks;
using Quartz;
using DaCollector.Abstractions.Video.Services;
using DaCollector.Server.Scheduling.Acquisition.Attributes;
using DaCollector.Server.Scheduling.Attributes;

namespace DaCollector.Server.Scheduling.Jobs.Actions;

[DatabaseRequired]
[JobKeyMember("ScanDropFolders")]
[JobKeyGroup(JobKeyGroup.Legacy)]
[DisallowConcurrentExecution]
internal class ScanDropFoldersJob : BaseJob
{
    private readonly IVideoService _videoService;

    public override string TypeName => "Scan Drop Folders";
    public override string Title => "Scanning Drop Folders";

    public override async Task Process()
    {
        await _videoService.ScheduleScanForManagedFolders(onlyDropSources: true);
    }

    public ScanDropFoldersJob(IVideoService videoService)
    {
        _videoService = videoService;
    }

    protected ScanDropFoldersJob() { }
}
