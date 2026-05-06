using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartz;
using DaCollector.Server.Collections;
using DaCollector.Server.Scheduling.Attributes;

namespace DaCollector.Server.Scheduling.Jobs.Collections;

[JobKeyMember("ManagedCollectionSync")]
[JobKeyGroup(JobKeyGroup.Actions)]
[DisallowConcurrentExecution]
public class SyncManagedCollectionsJob(ManagedCollectionSyncService syncService) : BaseJob
{
    public override string TypeName => "Sync Managed Collections";

    public override string Title => "Syncing Managed Collections";

    public override async Task Process()
    {
        var result = await syncService.Run(apply: true).ConfigureAwait(false);
        _logger.LogInformation(
            "Managed collection sync {RunID} evaluated {CollectionCount} enabled collections and {ItemCount} distinct items.",
            result.RunID,
            result.EnabledCollectionCount,
            result.TotalItemCount
        );

        foreach (var warning in result.Warnings)
            _logger.LogWarning("{Warning}", warning);

        foreach (var collection in result.Collections)
        {
            foreach (var warning in collection.Warnings)
                _logger.LogWarning("Managed collection {CollectionName}: {Warning}", collection.Collection.Name, warning);
        }
    }

    protected SyncManagedCollectionsJob() : this(null!) { }
}
