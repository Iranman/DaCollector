using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Collections;

#nullable enable
namespace Shoko.Server.Collections;

/// <summary>
/// Evaluates managed collection definitions for manual and scheduled sync runs.
/// </summary>
public class ManagedCollectionSyncService(ManagedCollectionService collectionService)
{
    public CollectionSyncRunResult Run(bool apply = false)
    {
        var runID = Guid.NewGuid();
        var startedAt = DateTimeOffset.UtcNow;
        var definitions = collectionService.GetAll();
        var enabled = definitions.Where(collection => collection.Enabled).ToList();
        var disabledCount = definitions.Count - enabled.Count;
        var results = new List<CollectionSyncResult>();
        var warnings = new List<string>();

        foreach (var definition in enabled)
        {
            try
            {
                results.Add(Evaluate(definition, apply));
            }
            catch (ArgumentException e)
            {
                results.Add(new()
                {
                    Collection = definition,
                    RequestedSyncMode = definition.SyncMode,
                    EffectiveSyncMode = CollectionSyncMode.Preview,
                    Target = "preview",
                    Warnings = [e.Message],
                });
            }
        }

        if (apply)
            warnings.Add("No collection target adapter is configured yet. The run evaluated collections in preview mode.");

        var finishedAt = DateTimeOffset.UtcNow;
        return new()
        {
            RunID = runID,
            StartedAt = startedAt,
            FinishedAt = finishedAt,
            EnabledCollectionCount = enabled.Count,
            DisabledCollectionCount = disabledCount,
            TotalItemCount = results
                .SelectMany(result => result.Items)
                .Select(item => item.ExternalID)
                .Distinct()
                .Count(),
            Collections = results,
            Warnings = warnings,
        };
    }

    public CollectionSyncResult? Run(Guid collectionID, bool apply = false)
    {
        var definition = collectionService.Get(collectionID);
        if (definition is null)
            return null;

        return Evaluate(definition, apply);
    }

    private CollectionSyncResult Evaluate(CollectionDefinition definition, bool apply)
    {
        var preview = collectionService.Preview(definition);
        var warnings = preview.Warnings.ToList();
        var effectiveMode = CollectionSyncMode.Preview;
        var target = "preview";

        if (apply && definition.SyncMode is not CollectionSyncMode.Preview)
            warnings.Add("No collection target adapter is configured yet. This collection was evaluated without applying membership changes.");

        return new()
        {
            Collection = preview.Collection,
            RequestedSyncMode = definition.SyncMode,
            EffectiveSyncMode = effectiveMode,
            Applied = false,
            Target = target,
            Items = preview.Items,
            Warnings = warnings,
        };
    }
}
