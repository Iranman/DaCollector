using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DaCollector.Abstractions.Collections;
using DaCollector.Server.Plex;
using DaCollector.Server.Settings;

#nullable enable
namespace DaCollector.Server.Collections;

/// <summary>
/// Evaluates managed collection definitions for manual and scheduled sync runs.
/// </summary>
public class ManagedCollectionSyncService(
    ManagedCollectionService collectionService,
    ISettingsProvider settingsProvider,
    PlexTargetService plexTargetService
)
{
    public async Task<CollectionSyncRunResult> Run(bool apply = false, CancellationToken cancellationToken = default)
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
                results.Add(await Evaluate(definition, apply, cancellationToken).ConfigureAwait(false));
            }
            catch (Exception e) when (e is ArgumentException or InvalidOperationException)
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

    public async Task<CollectionSyncResult?> Run(Guid collectionID, bool apply = false, CancellationToken cancellationToken = default)
    {
        var definition = collectionService.Get(collectionID);
        if (definition is null)
            return null;

        return await Evaluate(definition, apply, cancellationToken).ConfigureAwait(false);
    }

    private async Task<CollectionSyncResult> Evaluate(CollectionDefinition definition, bool apply, CancellationToken cancellationToken)
    {
        var preview = collectionService.Preview(definition);
        var warnings = preview.Warnings.ToList();
        var effectiveMode = CollectionSyncMode.Preview;
        var target = "preview";
        var applied = false;
        var matchedItemCount = 0;
        var missingItemCount = 0;
        var addedItemCount = 0;
        var removedItemCount = 0;

        if (apply && definition.SyncMode is not CollectionSyncMode.Preview)
        {
            var sectionKey = settingsProvider.GetSettings().Plex.TargetSectionKey;
            if (string.IsNullOrWhiteSpace(sectionKey))
            {
                warnings.Add("Plex target library section key is not configured. This collection was evaluated without applying membership changes.");
            }
            else
            {
                var applyResult = await plexTargetService
                    .ApplyCollection(sectionKey, preview.Collection.Name, preview.Items, definition.SyncMode, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                effectiveMode = definition.SyncMode;
                target = $"plex:{sectionKey}";
                applied = applyResult.Applied;
                matchedItemCount = applyResult.Match.Matched.Count;
                missingItemCount = applyResult.Match.Missing.Count;
                addedItemCount = applyResult.AddedItemCount;
                removedItemCount = applyResult.RemovedItemCount;
                warnings.AddRange(applyResult.Warnings);
            }
        }

        return new()
        {
            Collection = preview.Collection,
            RequestedSyncMode = definition.SyncMode,
            EffectiveSyncMode = effectiveMode,
            Applied = applied,
            Target = target,
            Items = preview.Items,
            MatchedItemCount = matchedItemCount,
            MissingItemCount = missingItemCount,
            AddedItemCount = addedItemCount,
            RemovedItemCount = removedItemCount,
            Warnings = warnings,
        };
    }
}
