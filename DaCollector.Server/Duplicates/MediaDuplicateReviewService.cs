using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DaCollector.Abstractions.Duplicates;
using DaCollector.Abstractions.MediaServers.Plex;
using DaCollector.Server.Plex;

#nullable enable
namespace DaCollector.Server.Duplicates;

/// <summary>
/// Finds possible duplicate media entries without mixing them with exact file duplicate cleanup.
/// </summary>
public class MediaDuplicateReviewService(PlexTargetService plexTargetService)
{
    public async Task<IReadOnlyList<MediaDuplicateSet>> GetPlexMediaDuplicates(
        string sectionKey,
        string? baseUrl = null,
        string? token = null,
        CancellationToken cancellationToken = default
    )
    {
        var items = await plexTargetService.GetLibraryItems(sectionKey, baseUrl, token, cancellationToken).ConfigureAwait(false);
        return FindDuplicateMediaEntries(items);
    }

    public static IReadOnlyList<MediaDuplicateSet> FindDuplicateMediaEntries(IReadOnlyList<PlexMediaItem> items)
    {
        var normalizedItems = items
            .Where(item => !string.IsNullOrWhiteSpace(item.RatingKey))
            .DistinctBy(item => item.RatingKey, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var signals = new List<DuplicateSignal>();

        AddPathHashSignals(normalizedItems, signals);
        AddProviderSignals(normalizedItems, signals);
        AddTitleYearSignals(normalizedItems, signals);

        return signals
            .GroupBy(signal => GetRatingKeySetKey(signal.Items))
            .Select(ToDuplicateSet)
            .Where(set => set.CandidateCount > 1)
            .OrderByDescending(set => set.Score)
            .ThenBy(set => set.Items.FirstOrDefault()?.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(set => set.DuplicateKey, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static MediaDuplicateSet ToDuplicateSet(IGrouping<string, DuplicateSignal> groupedSignals)
    {
        var signals = groupedSignals.ToList();
        var primary = signals
            .OrderByDescending(signal => signal.Score)
            .ThenBy(signal => signal.MatchType)
            .First();
        var items = primary.Items
            .OrderBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Year)
            .ThenBy(item => item.RatingKey, StringComparer.OrdinalIgnoreCase)
            .Select(ToReviewItem)
            .ToList();
        var ratingKeys = string.Join(", ", items.Select(item => item.RatingKey));
        var reasons = signals
            .Select(signal => signal.Reason)
            .Append($"Plex rating keys: {ratingKeys}.")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // An item is a safe-delete candidate if every file it holds also appears in at least
        // one other item in the set; removing it would not lose any files from the library.
        var safeDeleteCandidate = items.Any(item =>
            item.PathHashes.Count > 0 &&
            item.PathHashes.All(hash => items
                .Where(other => other.RatingKey != item.RatingKey)
                .SelectMany(other => other.PathHashes)
                .Contains(hash, StringComparer.OrdinalIgnoreCase)));

        var reviewAction = safeDeleteCandidate
            ? "One or more entries have all files covered by other entries; safe to remove duplicates from Plex."
            : "Review in Plex before deleting or merging media entries.";

        return new()
        {
            DuplicateKey = $"plex-media:{groupedSignals.Key}",
            PrimaryMatchType = primary.MatchType,
            Score = primary.Score,
            CandidateCount = items.Count,
            SafeDeleteCandidate = safeDeleteCandidate,
            ReviewAction = reviewAction,
            ScoringReasons = reasons,
            Items = items,
        };
    }

    private static MediaDuplicateItem ToReviewItem(PlexMediaItem item)
    {
        var pathHashes = item.FilePaths
            .Select(CreatePathHash)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new()
        {
            RatingKey = item.RatingKey,
            Title = item.Title,
            Type = item.Type,
            Year = item.Year,
            Guid = item.Guid,
            ExternalIDs = item.ExternalIDs,
            GuidValues = item.GuidValues,
            FilePaths = item.FilePaths,
            PathHashes = pathHashes,
        };
    }

    private static void AddPathHashSignals(IReadOnlyList<PlexMediaItem> items, List<DuplicateSignal> signals)
    {
        var groups = items
            .SelectMany(item => item.FilePaths
                .Select(CreatePathHash)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(pathHash => (pathHash, item)))
            .GroupBy(tuple => tuple.pathHash, StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            var groupItems = DistinctItems(group.Select(tuple => tuple.item));
            if (groupItems.Count > 1)
            {
                signals.Add(new(
                    MediaDuplicateMatchType.PathHash,
                    95,
                    $"Path hash match: {group.Key}.",
                    groupItems
                ));
            }
        }
    }

    private static void AddProviderSignals(IReadOnlyList<PlexMediaItem> items, List<DuplicateSignal> signals)
    {
        var groups = items
            .SelectMany(item => item.ExternalIDs
                .Select(externalID => (externalID: externalID.ToString(), item)))
            .GroupBy(tuple => tuple.externalID, StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            var groupItems = DistinctItems(group.Select(tuple => tuple.item));
            if (groupItems.Count > 1)
            {
                signals.Add(new(
                    MediaDuplicateMatchType.ProviderID,
                    100,
                    $"Provider ID match: {group.Key}.",
                    groupItems
                ));
            }
        }
    }

    private static void AddTitleYearSignals(IReadOnlyList<PlexMediaItem> items, List<DuplicateSignal> signals)
    {
        var groups = items
            .Where(item => item.Year.HasValue && !string.IsNullOrWhiteSpace(item.Title))
            .GroupBy(item => $"{NormalizeTitle(item.Title)}:{item.Year}", StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            var groupItems = DistinctItems(group);
            if (groupItems.Count > 1)
            {
                var sample = groupItems.First();
                signals.Add(new(
                    MediaDuplicateMatchType.TitleYear,
                    60,
                    $"Title/year match: {sample.Title} ({sample.Year}).",
                    groupItems
                ));
            }
        }
    }

    private static IReadOnlyList<PlexMediaItem> DistinctItems(IEnumerable<PlexMediaItem> items) =>
        items
            .Where(item => !string.IsNullOrWhiteSpace(item.RatingKey))
            .DistinctBy(item => item.RatingKey, StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item.RatingKey, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static string GetRatingKeySetKey(IReadOnlyList<PlexMediaItem> items) =>
        string.Join("|", items.Select(item => item.RatingKey).OrderBy(ratingKey => ratingKey, StringComparer.OrdinalIgnoreCase));

    private static string NormalizeTitle(string title) =>
        string.Join(" ", title.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private static string CreatePathHash(string path)
    {
        var normalized = path.Trim().Replace('\\', '/').ToLowerInvariant();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }

    private sealed record DuplicateSignal(
        MediaDuplicateMatchType MatchType,
        int Score,
        string Reason,
        IReadOnlyList<PlexMediaItem> Items
    );
}
