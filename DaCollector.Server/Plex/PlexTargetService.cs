using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using DaCollector.Abstractions.Collections;
using DaCollector.Abstractions.MediaServers.Plex;
using DaCollector.Abstractions.Metadata;
using DaCollector.Abstractions.Metadata.Enums;
using DaCollector.Server.Settings;

#nullable enable
namespace DaCollector.Server.Plex;

/// <summary>
/// Direct Plex server adapter used by DaCollector collection manager.
/// </summary>
public class PlexTargetService(ISettingsProvider settingsProvider, IHttpClientFactory httpClientFactory, ILogger<PlexTargetService> logger)
{
    public async Task<PlexServerIdentity> GetIdentity(string? baseUrl = null, CancellationToken cancellationToken = default)
    {
        var targetUrl = GetBaseUrl(baseUrl);
        logger.LogDebug("Plex identity check: {Url}", targetUrl + "identity");
        try
        {
            using var response = await Send(targetUrl, "/identity", null, cancellationToken).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var document = XDocument.Parse(content);
            var container = document.Root;

            var identity = new PlexServerIdentity
            {
                BaseUrl = targetUrl,
                Reachable = response.IsSuccessStatusCode,
                StatusCode = (int)response.StatusCode,
                Status = response.StatusCode.ToString(),
                MachineIdentifier = GetAttribute(container, "machineIdentifier"),
                Version = GetAttribute(container, "version"),
                ApiVersion = GetAttribute(container, "apiVersion"),
                Claimed = ParseBoolean(GetAttribute(container, "claimed")),
            };

            if (identity.Reachable)
                logger.LogInformation("Plex reachable at {Url} (version {Version}, machine {MachineID})",
                    targetUrl, identity.Version, identity.MachineIdentifier);
            else
                logger.LogWarning("Plex at {Url} returned HTTP {StatusCode}", targetUrl, identity.StatusCode);

            return identity;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or UriFormatException or System.Xml.XmlException)
        {
            logger.LogWarning("Plex identity check failed for {Url}: {Error}", targetUrl, e.Message);
            return new()
            {
                BaseUrl = targetUrl,
                Reachable = false,
                Status = e.Message,
            };
        }
    }

    public async Task<IReadOnlyList<PlexLibrarySection>> GetLibraries(string? baseUrl = null, string? token = null, CancellationToken cancellationToken = default)
    {
        var targetUrl = GetBaseUrl(baseUrl);
        var targetToken = GetToken(token);
        if (string.IsNullOrWhiteSpace(targetToken))
            throw new InvalidOperationException("A Plex token is required to read library sections.");

        logger.LogDebug("Reading Plex library sections from {Url}", targetUrl);
        using var response = await Send(targetUrl, "/library/sections", targetToken, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Plex returned HTTP {StatusCode} reading library sections from {Url}", (int)response.StatusCode, targetUrl);
            throw new InvalidOperationException($"Plex returned {(int)response.StatusCode} {response.StatusCode} when reading library sections.");
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var document = XDocument.Parse(content);
        var sections = document
            .Descendants("Directory")
            .Select(directory => new PlexLibrarySection
            {
                Key = GetAttribute(directory, "key") ?? string.Empty,
                Title = GetAttribute(directory, "title") ?? string.Empty,
                Type = GetAttribute(directory, "type") ?? string.Empty,
                Scanner = GetAttribute(directory, "scanner"),
                Agent = GetAttribute(directory, "agent"),
                Language = GetAttribute(directory, "language"),
            })
            .Where(section => section.Key.Length > 0)
            .OrderBy(section => section.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        logger.LogInformation("Found {Count} Plex library section(s): {Sections}",
            sections.Count, string.Join(", ", sections.Select(s => $"{s.Key}:{s.Title}({s.Type})")));
        return sections;
    }

    public async Task<IReadOnlyList<PlexMediaItem>> GetLibraryItems(string sectionKey, string? baseUrl = null, string? token = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sectionKey))
            throw new ArgumentException("Plex library section key is required.", nameof(sectionKey));

        var targetUrl = GetBaseUrl(baseUrl);
        var targetToken = GetToken(token);
        if (string.IsNullOrWhiteSpace(targetToken))
            throw new InvalidOperationException("A Plex token is required to read library items.");

        const int PageSize = 200;
        var items = new List<PlexMediaItem>();
        var start = 0;
        var total = 1;

        logger.LogDebug("Loading Plex library items for section {SectionKey}", sectionKey);
        while (start < total)
        {
            var path = $"/library/sections/{Uri.EscapeDataString(sectionKey)}/all?includeGuids=1";
            using var response = await Send(targetUrl, path, targetToken, cancellationToken, containerStart: start, containerSize: PageSize).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Plex returned HTTP {StatusCode} reading library items for section {SectionKey}", (int)response.StatusCode, sectionKey);
                throw new InvalidOperationException($"Plex returned {(int)response.StatusCode} {response.StatusCode} when reading library items.");
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var document = XDocument.Parse(content);
            total = ParseInt(GetAttribute(document.Root, "totalSize")) ?? ParseInt(GetAttribute(document.Root, "size")) ?? items.Count;
            var pageItems = document
                .Root?
                .Elements()
                .Where(element => element.Name.LocalName is "Video" or "Directory")
                .Select(ToMediaItem)
                .Where(item => item.RatingKey.Length > 0)
                .ToList() ?? [];

            items.AddRange(pageItems);
            if (pageItems.Count == 0)
                break;

            start += PageSize;
        }

        logger.LogInformation("Loaded {Count} item(s) from Plex library section {SectionKey}", items.Count, sectionKey);
        return items;
    }

    public async Task<PlexCollectionMatch> MatchItems(
        string sectionKey,
        IReadOnlyList<CollectionBuilderPreviewItem> targetItems,
        string? baseUrl = null,
        string? token = null,
        CancellationToken cancellationToken = default
    )
    {
        var libraryItems = await GetLibraryItems(sectionKey, baseUrl, token, cancellationToken).ConfigureAwait(false);
        var itemsByExternalID = libraryItems
            .SelectMany(item => item.ExternalIDs.Select(externalID => (externalID, item)))
            .GroupBy(tuple => tuple.externalID)
            .ToDictionary(group => group.Key, group => group.First().item);

        var matched = new List<PlexCollectionMatchedItem>();
        var missing = new List<CollectionBuilderPreviewItem>();
        foreach (var target in targetItems)
        {
            if (itemsByExternalID.TryGetValue(target.ExternalID, out var plexItem))
            {
                matched.Add(new()
                {
                    Target = target,
                    PlexItem = plexItem,
                });
                continue;
            }

            missing.Add(target);
        }

        var noIDCount = libraryItems.Count(item => item.ExternalIDs.Count == 0);
        var matchWarnings = noIDCount > 0
            ? (IReadOnlyList<string>)[$"{noIDCount} library item(s) have no provider IDs and cannot be matched to collection targets."]
            : [];

        return new()
        {
            SectionKey = sectionKey,
            Matched = matched,
            Missing = missing,
            TargetItemCount = targetItems.Count,
            Warnings = matchWarnings,
        };
    }

    public async Task<PlexCollectionApplyResult> ApplyCollection(
        string sectionKey,
        string collectionName,
        IReadOnlyList<CollectionBuilderPreviewItem> targetItems,
        CollectionSyncMode syncMode,
        bool dryRun = false,
        string? baseUrl = null,
        string? token = null,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(collectionName))
            throw new ArgumentException("Collection name is required.", nameof(collectionName));

        var normalizedName = collectionName.Trim();
        var match = await MatchItems(sectionKey, targetItems, baseUrl, token, cancellationToken).ConfigureAwait(false);
        if (syncMode is CollectionSyncMode.Preview)
        {
            return new()
            {
                SectionKey = sectionKey,
                CollectionName = normalizedName,
                SyncMode = syncMode,
                Applied = false,
                Match = match,
                Warnings = ["Collection was evaluated in preview mode; no Plex changes were applied."],
            };
        }

        var targetUrl = GetBaseUrl(baseUrl);
        var targetToken = GetToken(token);
        if (string.IsNullOrWhiteSpace(targetToken))
            throw new InvalidOperationException("A Plex token is required to apply collection changes.");

        var existingItems = await GetCollectionItems(sectionKey, normalizedName, targetUrl, targetToken, cancellationToken).ConfigureAwait(false);
        var existingByKey = existingItems
            .Where(item => item.RatingKey.Length > 0)
            .ToDictionary(item => item.RatingKey, StringComparer.OrdinalIgnoreCase);
        var matchedItems = match.Matched
            .Select(item => item.PlexItem)
            .Where(item => item.RatingKey.Length > 0)
            .DistinctBy(item => item.RatingKey, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var matchedKeys = matchedItems
            .Select(item => item.RatingKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var itemsToAdd = matchedItems
            .Where(item => !existingByKey.ContainsKey(item.RatingKey))
            .ToList();
        var itemsToRemove = syncMode is CollectionSyncMode.Sync
            ? existingItems.Where(item => !matchedKeys.Contains(item.RatingKey)).ToList()
            : [];

        logger.LogInformation(
            "Collection '{Name}' ({Mode}): {Matched} matched, {Missing} missing, {ToAdd} to add, {ToRemove} to remove, {Unchanged} unchanged{DryRunNote}",
            normalizedName, syncMode, match.Matched.Count, match.Missing.Count, itemsToAdd.Count, itemsToRemove.Count,
            matchedItems.Count - itemsToAdd.Count, dryRun ? " [dry run — not writing to Plex]" : string.Empty);

        if (!dryRun)
        {
            await UpdateCollectionMembership(sectionKey, normalizedName, itemsToAdd, add: true, targetUrl, targetToken, cancellationToken).ConfigureAwait(false);
            await UpdateCollectionMembership(sectionKey, normalizedName, itemsToRemove, add: false, targetUrl, targetToken, cancellationToken).ConfigureAwait(false);
            if (itemsToAdd.Count > 0 || itemsToRemove.Count > 0)
                logger.LogInformation("Applied collection '{Name}': added {Added}, removed {Removed}", normalizedName, itemsToAdd.Count, itemsToRemove.Count);
        }

        return new()
        {
            SectionKey = sectionKey,
            CollectionName = normalizedName,
            SyncMode = syncMode,
            Applied = !dryRun && (itemsToAdd.Count > 0 || itemsToRemove.Count > 0),
            DryRun = dryRun,
            Match = match,
            ExistingItemCount = existingItems.Count,
            AddedItemCount = itemsToAdd.Count,
            RemovedItemCount = itemsToRemove.Count,
            UnchangedItemCount = matchedItems.Count - itemsToAdd.Count,
            Added = itemsToAdd,
            Removed = itemsToRemove,
        };
    }

    private async Task<IReadOnlyList<PlexMediaItem>> GetCollectionItems(string sectionKey, string collectionName, string baseUrl, string token, CancellationToken cancellationToken)
    {
        var collectionRatingKey = await FindCollectionRatingKey(sectionKey, collectionName, baseUrl, token, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(collectionRatingKey))
            return [];

        const int PageSize = 200;
        var items = new List<PlexMediaItem>();
        var start = 0;
        var total = 1;

        while (start < total)
        {
            var path = $"/library/collections/{Uri.EscapeDataString(collectionRatingKey)}/children?includeGuids=1";
            using var response = await Send(baseUrl, path, token, cancellationToken, containerStart: start, containerSize: PageSize).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Plex returned {(int)response.StatusCode} {response.StatusCode} when reading collection items.");

            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var document = XDocument.Parse(content);
            total = ParseInt(GetAttribute(document.Root, "totalSize")) ?? ParseInt(GetAttribute(document.Root, "size")) ?? items.Count;
            var pageItems = document
                .Root?
                .Elements()
                .Where(element => element.Name.LocalName is "Video" or "Directory")
                .Select(ToMediaItem)
                .Where(item => item.RatingKey.Length > 0)
                .ToList() ?? [];

            items.AddRange(pageItems);
            if (pageItems.Count == 0)
                break;

            start += PageSize;
        }

        return items;
    }

    private async Task<string?> FindCollectionRatingKey(string sectionKey, string collectionName, string baseUrl, string token, CancellationToken cancellationToken)
    {
        const int PageSize = 200;
        var start = 0;
        var total = 1;

        while (start < total)
        {
            var path = BuildPath($"/library/sections/{Uri.EscapeDataString(sectionKey)}/all", [new("type", "18")]);
            using var response = await Send(baseUrl, path, token, cancellationToken, containerStart: start, containerSize: PageSize).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Plex returned {(int)response.StatusCode} {response.StatusCode} when reading collections.");

            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var document = XDocument.Parse(content);
            total = ParseInt(GetAttribute(document.Root, "totalSize")) ?? ParseInt(GetAttribute(document.Root, "size")) ?? 0;
            var ratingKey = document
                .Root?
                .Elements("Directory")
                .Where(directory => string.Equals(GetAttribute(directory, "title"), collectionName, StringComparison.OrdinalIgnoreCase))
                .Select(directory => GetAttribute(directory, "ratingKey") ?? GetAttribute(directory, "key"))
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
            if (!string.IsNullOrWhiteSpace(ratingKey))
                return ratingKey;

            start += PageSize;
        }

        return null;
    }

    private async Task UpdateCollectionMembership(
        string sectionKey,
        string collectionName,
        IReadOnlyList<PlexMediaItem> items,
        bool add,
        string baseUrl,
        string token,
        CancellationToken cancellationToken
    )
    {
        const int BatchSize = 100;

        foreach (var group in items.GroupBy(item => ToPlexSearchType(item.Type)))
        {
            if (group.Key is null)
                continue;

            var ratingKeys = group
                .Select(item => item.RatingKey)
                .Where(ratingKey => ratingKey.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (ratingKeys.Count == 0)
                continue;

            for (var offset = 0; offset < ratingKeys.Count; offset += BatchSize)
            {
                var batch = ratingKeys.Skip(offset).Take(BatchSize).ToList();
                logger.LogDebug("{Action} {Count} item(s) of type {Type} in collection '{Collection}'",
                    add ? "Adding" : "Removing", batch.Count, group.Key, collectionName);
                var query = add
                    ? new List<KeyValuePair<string, string>>
                    {
                        new("collection.locked", "1"),
                        new("collection[0].tag.tag", collectionName),
                        new("id", string.Join(",", batch)),
                        new("type", group.Key),
                    }
                    : [
                        new("collection.locked", "1"),
                        new("collection[].tag.tag-", collectionName),
                        new("id", string.Join(",", batch)),
                        new("type", group.Key),
                    ];
                var path = BuildPath($"/library/sections/{Uri.EscapeDataString(sectionKey)}/all", query);
                using var response = await Send(baseUrl, path, token, cancellationToken, method: HttpMethod.Put).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    var action = add ? "adding items to" : "removing items from";
                    logger.LogError("Plex returned HTTP {StatusCode} when {Action} collection '{Collection}'", (int)response.StatusCode, action, collectionName);
                    throw new InvalidOperationException($"Plex returned {(int)response.StatusCode} {response.StatusCode} when {action} collection '{collectionName}'.");
                }
            }
        }
    }

    private async Task<HttpResponseMessage> Send(
        string baseUrl,
        string path,
        string? token,
        CancellationToken cancellationToken,
        HttpMethod? method = null,
        int? containerStart = null,
        int? containerSize = null
    )
    {
        var client = httpClientFactory.CreateClient("PlexTarget");
        var requestUri = new Uri(new Uri(baseUrl), path);
        var request = new HttpRequestMessage(method ?? HttpMethod.Get, requestUri);
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Add("X-Plex-Token", token);
        if (containerStart.HasValue)
            request.Headers.Add("X-Plex-Container-Start", containerStart.Value.ToString());
        if (containerSize.HasValue)
            request.Headers.Add("X-Plex-Container-Size", containerSize.Value.ToString());

        return await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private string GetBaseUrl(string? overrideBaseUrl)
    {
        var value = !string.IsNullOrWhiteSpace(overrideBaseUrl)
            ? overrideBaseUrl
            : settingsProvider.GetSettings().Plex.TargetBaseUrl;
        value = (value ?? string.Empty).Trim();
        if (value.Length == 0)
            value = "http://127.0.0.1:32400";
        if (!value.Contains("://", StringComparison.Ordinal))
            value = "http://" + value;

        return value.TrimEnd('/') + "/";
    }

    private string GetToken(string? overrideToken) =>
        !string.IsNullOrWhiteSpace(overrideToken)
            ? overrideToken.Trim()
            : settingsProvider.GetSettings().Plex.TargetToken;

    private static string BuildPath(string path, IReadOnlyList<KeyValuePair<string, string>> query)
    {
        if (query.Count == 0)
            return path;

        return path + "?" + string.Join("&", query.Select(parameter => $"{parameter.Key}={Uri.EscapeDataString(parameter.Value)}"));
    }

    private static string? GetAttribute(XElement? element, string name) =>
        element?.Attribute(name)?.Value;

    private static bool? ParseBoolean(string? value) =>
        value switch
        {
            "1" => true,
            "0" => false,
            _ when bool.TryParse(value, out var result) => result,
            _ => null,
        };

    private static PlexMediaItem ToMediaItem(XElement element)
    {
        var type = GetAttribute(element, "type") ?? string.Empty;
        var externalIDs = element
            .Elements("Guid")
            .Select(guid => GetAttribute(guid, "id"))
            .Where(guid => !string.IsNullOrWhiteSpace(guid))
            .Select(guid => TryParseExternalID(guid!, type))
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToList();
        var guidValues = element
            .Elements("Guid")
            .Select(guid => GetAttribute(guid, "id"))
            .Where(guid => !string.IsNullOrWhiteSpace(guid))
            .Select(guid => guid!)
            .ToList();
        var filePaths = element
            .Descendants("Part")
            .Select(part => GetAttribute(part, "file"))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new()
        {
            RatingKey = GetAttribute(element, "ratingKey") ?? string.Empty,
            Title = GetAttribute(element, "title") ?? string.Empty,
            Type = type,
            Year = ParseInt(GetAttribute(element, "year")),
            Guid = GetAttribute(element, "guid"),
            ExternalIDs = externalIDs,
            GuidValues = guidValues,
            FilePaths = filePaths,
        };
    }

    private static ExternalMediaId? TryParseExternalID(string guid, string plexType)
    {
        var parts = guid.Split("://", 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
            return null;

        var kind = ToMediaKind(plexType);
        if (kind is MediaKind.Unknown)
            return null;

        return parts[0].ToLowerInvariant() switch
        {
            "imdb" => ExternalMediaId.ImdbTitle(parts[1], kind),
            "tmdb" when int.TryParse(parts[1], out var tmdbID) && kind is MediaKind.Movie => ExternalMediaId.TmdbMovie(tmdbID),
            "tmdb" when int.TryParse(parts[1], out var tmdbID) && kind is MediaKind.Show => ExternalMediaId.TmdbShow(tmdbID),
            "tvdb" when int.TryParse(parts[1], out var tvdbID) && kind is MediaKind.Movie => ExternalMediaId.TvdbMovie(tvdbID),
            "tvdb" when int.TryParse(parts[1], out var tvdbID) && kind is MediaKind.Show => ExternalMediaId.TvdbShow(tvdbID),
            _ => null,
        };
    }

    private static MediaKind ToMediaKind(string plexType) =>
        plexType.ToLowerInvariant() switch
        {
            "movie" => MediaKind.Movie,
            "show" => MediaKind.Show,
            _ => MediaKind.Unknown,
        };

    private static string? ToPlexSearchType(string plexType) =>
        plexType.ToLowerInvariant() switch
        {
            "movie" => "1",
            "show" => "2",
            _ => null,
        };

    private static int? ParseInt(string? value) =>
        int.TryParse(value, out var result) ? result : null;
}
