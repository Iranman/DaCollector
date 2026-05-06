using System.Collections.Generic;
using System.Threading.Tasks;
using Quartz;
using DaCollector.Server.Scheduling.Acquisition.Attributes;
using DaCollector.Server.Scheduling.Attributes;
using DaCollector.Server.Services;

namespace DaCollector.Server.Scheduling.Jobs.Actions;

[DatabaseRequired]
[JobKeyMember("RemoveMissingFiles")]
[JobKeyGroup(JobKeyGroup.Legacy)]
[DisallowConcurrentExecution]
internal class RemoveMissingFilesJob : BaseJob
{
    private readonly ActionService _actionService;

    [JobKeyMember]
    public bool RemoveMyList { get; set; }
    public override string TypeName => "Remove Missing Files";
    public override string Title => "Removing Missing Files";
    public override Dictionary<string, object> Details => new()
    {
        {
            "Remove From MyList", RemoveMyList
        }
    };

    public override async Task Process()
    {
        await _actionService.RemoveRecordsWithoutPhysicalFiles(RemoveMyList);
    }

    public RemoveMissingFilesJob(ActionService actionService)
    {
        _actionService = actionService;
    }

    protected RemoveMissingFilesJob() { }
}
