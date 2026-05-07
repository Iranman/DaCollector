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
            "Managed collection sync {RunID} completed in {Elapsed:g}: {EnabledCount} enabled, {DisabledCount} disabled, {ItemCount} distinct items.",
            result.RunID,
            result.FinishedAt - result.StartedAt,
            result.EnabledCollectionCount,
            result.DisabledCollectionCount,
            result.TotalItemCount
        );

        foreach (var warning in result.Warnings)
            _logger.LogWarning("{Warning}", warning);

        foreach (var collection in result.Collections)
        {
            if (collection.Applied)
                _logger.LogInformation(
                    "Collection '{Name}' ({Mode} → {Target}): {Matched} matched, {Missing} missing, +{Added} added, -{Removed} removed.",
                    collection.Collection.Name,
                    collection.EffectiveSyncMode,
                    collection.Target,
                    collection.MatchedItemCount,
                    collection.MissingItemCount,
                    collection.AddedItemCount,
                    collection.RemovedItemCount
                );
            else
                _logger.LogInformation(
                    "Collection '{Name}' ({Mode}): {ItemCount} items resolved (not applied).",
                    collection.Collection.Name,
                    collection.EffectiveSyncMode,
                    collection.Items.Count
                );

            foreach (var warning in collection.Warnings)
                _logger.LogWarning("Collection '{Name}': {Warning}", collection.Collection.Name, warning);
        }
    }

    protected SyncManagedCollectionsJob() : this(null!) { }
}
