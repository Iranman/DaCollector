using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Collections;
using Shoko.Server.Settings;

#nullable enable
namespace Shoko.Server.Collections;

/// <summary>
/// Stores and previews The Collector managed collection definitions.
/// </summary>
public class ManagedCollectionService(ISettingsProvider settingsProvider, CollectionBuilderPreviewService previewService)
{
    private readonly object _syncRoot = new();

    public IReadOnlyList<CollectionDefinition> GetAll() =>
        settingsProvider.GetSettings()
            .CollectionManager
            .Collections
            .OrderBy(collection => collection.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public CollectionDefinition? Get(Guid collectionID) =>
        settingsProvider.GetSettings()
            .CollectionManager
            .Collections
            .FirstOrDefault(collection => collection.ID == collectionID);

    public CollectionDefinition Create(CollectionDefinition definition)
    {
        lock (_syncRoot)
        {
            var settings = settingsProvider.GetSettings(copy: true);
            var collection = Normalize(definition, definition.ID == Guid.Empty ? Guid.NewGuid() : definition.ID);
            Validate(collection);

            if (settings.CollectionManager.Collections.Any(existing => existing.ID == collection.ID))
                throw new ArgumentException($"A managed collection with ID '{collection.ID}' already exists.");

            settings.CollectionManager.Collections.Add(collection);
            settingsProvider.SaveSettings(settings);
            return collection;
        }
    }

    public CollectionDefinition? Update(Guid collectionID, CollectionDefinition definition)
    {
        lock (_syncRoot)
        {
            var settings = settingsProvider.GetSettings(copy: true);
            var collections = settings.CollectionManager.Collections;
            var index = collections.FindIndex(collection => collection.ID == collectionID);
            if (index < 0)
                return null;

            var collection = Normalize(definition, collectionID);
            Validate(collection);
            collections[index] = collection;
            settingsProvider.SaveSettings(settings);
            return collection;
        }
    }

    public bool Delete(Guid collectionID)
    {
        lock (_syncRoot)
        {
            var settings = settingsProvider.GetSettings(copy: true);
            var removed = settings.CollectionManager.Collections.RemoveAll(collection => collection.ID == collectionID) > 0;
            if (removed)
                settingsProvider.SaveSettings(settings);
            return removed;
        }
    }

    public CollectionPreview? Preview(Guid collectionID)
    {
        var collection = Get(collectionID);
        return collection is null ? null : Preview(collection);
    }

    public CollectionPreview Preview(CollectionDefinition definition)
    {
        var collection = Normalize(definition, definition.ID == Guid.Empty ? Guid.NewGuid() : definition.ID);
        Validate(collection);

        var items = new List<CollectionBuilderPreviewItem>();
        var warnings = new List<string>();

        foreach (var rule in collection.Rules)
        {
            var preview = previewService.Preview(rule);
            items.AddRange(preview.Items);
            warnings.AddRange(preview.Warnings.Select(warning => $"{rule.Builder}: {warning}"));
        }

        return new()
        {
            Collection = collection,
            Items = items
                .GroupBy(item => item.ExternalID)
                .Select(group => group.First())
                .ToList(),
            Warnings = warnings,
        };
    }

    private static CollectionDefinition Normalize(CollectionDefinition definition, Guid collectionID)
    {
        var rules = (definition.Rules ?? [])
            .Select(rule => rule with
            {
                Builder = rule.Builder.Trim(),
                Options = NormalizeOptions(rule.Options),
            })
            .ToList();

        return definition with
        {
            ID = collectionID,
            Name = definition.Name.Trim(),
            Rules = rules,
        };
    }

    private static IReadOnlyDictionary<string, string> NormalizeOptions(IReadOnlyDictionary<string, string>? options) =>
        (options ?? new Dictionary<string, string>())
            .Where(kv => !string.IsNullOrWhiteSpace(kv.Key))
            .ToDictionary(kv => kv.Key.Trim(), kv => kv.Value?.Trim() ?? string.Empty, StringComparer.OrdinalIgnoreCase);

    private static void Validate(CollectionDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.Name))
            throw new ArgumentException("Collection name cannot be empty.");

        if (definition.Rules.Count == 0)
            throw new ArgumentException("A managed collection must contain at least one rule.");

        foreach (var rule in definition.Rules)
        {
            if (string.IsNullOrWhiteSpace(rule.Builder))
                throw new ArgumentException("Collection rule builder cannot be empty.");

            if (!CollectionBuilderCatalog.TryGet(rule.Builder, out _))
                throw new ArgumentException($"Unknown collection builder '{rule.Builder}'.");
        }
    }
}
