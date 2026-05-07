using System;
using System.Collections.Generic;
using System.Linq;
using DaCollector.Abstractions.Duplicates;
using DaCollector.Server.Models.DaCollector;
using DaCollector.Server.Repositories;

#nullable enable
namespace DaCollector.Server.Duplicates;

/// <summary>
/// Finds exact duplicate video locations using stored hash and file size data.
/// </summary>
public class ExactDuplicateService
{
    public IReadOnlyList<ExactDuplicateSet> GetExactDuplicates(
        bool includeIgnored = false,
        bool onlyAvailable = false,
        int? preferredManagedFolderID = null,
        string? preferredPathContains = null
    )
    {
        preferredPathContains = string.IsNullOrWhiteSpace(preferredPathContains) ? null : preferredPathContains.Trim();

        var groups = RepoFactory.VideoLocal.GetAll()
            .Where(video => includeIgnored || !video.IsIgnored)
            .Where(video => !string.IsNullOrWhiteSpace(video.Hash) && video.FileSize > 0)
            .Select(video => (video, locations: GetLocations(video, onlyAvailable)))
            .Where(tuple => tuple.locations.Count > 0)
            .GroupBy(tuple => new ExactDuplicateKey(tuple.video.Hash, tuple.video.FileSize))
            .Where(group => group.Sum(tuple => tuple.locations.Count) > 1)
            .OrderByDescending(group => group.Key.FileSize * Math.Max(0, group.Sum(tuple => tuple.locations.Count) - 1))
            .ThenBy(group => group.Key.Hash, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return groups
            .Select(group => ToDuplicateSet(group, preferredManagedFolderID, preferredPathContains))
            .ToList();
    }

    public IReadOnlyList<ExactDuplicateCleanupPlan> GetCleanupPlans(
        bool includeIgnored = false,
        bool onlyAvailable = false,
        int? preferredManagedFolderID = null,
        string? preferredPathContains = null
    ) =>
        GetExactDuplicates(includeIgnored, onlyAvailable, preferredManagedFolderID, preferredPathContains)
            .Select(ToCleanupPlan)
            .ToList();

    public ExactDuplicateSummary GetSummary(
        bool includeIgnored = false,
        bool onlyAvailable = false,
        int? preferredManagedFolderID = null,
        string? preferredPathContains = null
    )
    {
        var sets = GetExactDuplicates(includeIgnored, onlyAvailable, preferredManagedFolderID, preferredPathContains);
        var plans = sets.Select(ToCleanupPlan).ToList();
        return new()
        {
            SetCount = sets.Count,
            LocationCount = sets.Sum(set => set.LocationCount),
            SuggestedRemoveLocationCount = sets.Sum(set => set.SuggestedRemoveLocationIDs.Count),
            PotentialReclaimBytes = sets.Sum(set => set.FileSize * set.SuggestedRemoveLocationIDs.Count),
            AvailablePotentialReclaimBytes = plans.Sum(plan => plan.PotentialReclaimBytes),
            AvailableLocationCount = sets.Sum(set => set.AvailableLocationCount),
            UnavailableRemoveLocationCount = plans.Sum(plan => plan.RemoveCandidateCount - plan.AvailableRemoveCandidateCount),
        };
    }

    private static IReadOnlyList<VideoLocal_Place> GetLocations(VideoLocal video, bool onlyAvailable)
    {
        var locations = video.Places
            .Where(place => place.ManagedFolderID > 0 && !string.IsNullOrWhiteSpace(place.RelativePath));
        if (onlyAvailable)
            locations = locations.Where(place => place.IsAvailable);

        return locations
            .OrderByDescending(place => place.IsAvailable)
            .ThenBy(place => place.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(place => place.ID)
            .ToList();
    }

    private static ExactDuplicateSet ToDuplicateSet(
        IGrouping<ExactDuplicateKey, (VideoLocal video, IReadOnlyList<VideoLocal_Place> locations)> group,
        int? preferredManagedFolderID,
        string? preferredPathContains
    )
    {
        var flattened = group
            .SelectMany(tuple => tuple.locations.Select(location => (tuple.video, location)))
            .OrderByDescending(tuple => tuple.location.IsAvailable)
            .ThenByDescending(tuple => IsPreferredManagedFolder(tuple.location, preferredManagedFolderID))
            .ThenByDescending(tuple => IsPreferredPath(tuple.location, preferredPathContains))
            .ThenByDescending(tuple => tuple.video.DateTimeImported.HasValue)
            .ThenBy(tuple => tuple.video.DateTimeImported ?? tuple.video.DateTimeCreated)
            .ThenBy(tuple => tuple.location.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(tuple => tuple.location.ID)
            .ToList();
        var keepLocationID = flattened.FirstOrDefault().location?.ID;
        var removeLocationIDs = flattened
            .Where(tuple => tuple.location.ID != keepLocationID)
            .Select(tuple => tuple.location.ID)
            .ToList();

        return new()
        {
            DuplicateKey = $"{group.Key.Hash}:{group.Key.FileSize}",
            Hash = group.Key.Hash,
            FileSize = group.Key.FileSize,
            LocationCount = flattened.Count,
            AvailableLocationCount = flattened.Count(tuple => tuple.location.IsAvailable),
            VideoIDs = flattened
                .Select(tuple => tuple.video.VideoLocalID)
                .Distinct()
                .ToList(),
            SuggestedKeepLocationID = keepLocationID,
            SuggestedRemoveLocationIDs = removeLocationIDs,
            Locations = flattened
                .Select(tuple => ToLocation(tuple.video, tuple.location, keepLocationID, preferredManagedFolderID, preferredPathContains))
                .ToList(),
        };
    }

    private static ExactDuplicateCleanupPlan ToCleanupPlan(ExactDuplicateSet set)
    {
        var keepLocation = set.Locations.FirstOrDefault(location => location.LocationID == set.SuggestedKeepLocationID);
        var removeCandidates = set.Locations
            .Where(location => location.SuggestedRemove)
            .ToList();
        var warnings = new List<string>();

        if (keepLocation is null)
            warnings.Add("No keep location could be selected for this duplicate set.");

        if (removeCandidates.Any(location => !location.IsAvailable))
            warnings.Add("Some remove candidates are unavailable; reclaim bytes count only files that currently exist on disk.");

        return new()
        {
            DuplicateKey = set.DuplicateKey,
            HashType = set.HashType,
            Hash = set.Hash,
            FileSize = set.FileSize,
            LocationCount = set.LocationCount,
            AvailableLocationCount = set.AvailableLocationCount,
            KeepLocation = keepLocation,
            RemoveCandidates = removeCandidates,
            RemoveCandidateCount = removeCandidates.Count,
            AvailableRemoveCandidateCount = removeCandidates.Count(location => location.IsAvailable),
            PotentialReclaimBytes = set.FileSize * removeCandidates.Count(location => location.IsAvailable),
            Warnings = warnings,
        };
    }

    private static ExactDuplicateLocation ToLocation(
        VideoLocal video,
        VideoLocal_Place location,
        int? keepLocationID,
        int? preferredManagedFolderID,
        string? preferredPathContains
    )
    {
        var managedFolder = location.ManagedFolder;
        var suggestedKeep = location.ID == keepLocationID;
        return new()
        {
            VideoID = video.VideoLocalID,
            LocationID = location.ID,
            ManagedFolderID = location.ManagedFolderID,
            ManagedFolderName = managedFolder?.Name ?? string.Empty,
            RelativePath = location.RelativePath,
            Path = location.Path,
            FileName = location.FileName,
            IsAvailable = location.IsAvailable,
            IsIgnored = video.IsIgnored,
            SuggestedKeep = suggestedKeep,
            SuggestedRemove = !suggestedKeep,
            SelectionReason = GetSelectionReason(video, location, suggestedKeep, preferredManagedFolderID, preferredPathContains),
            ImportedAt = video.DateTimeImported,
            CreatedAt = video.DateTimeCreated,
        };
    }

    private static string GetSelectionReason(
        VideoLocal video,
        VideoLocal_Place location,
        bool suggestedKeep,
        int? preferredManagedFolderID,
        string? preferredPathContains
    )
    {
        if (!suggestedKeep)
        {
            if (!location.IsAvailable)
                return "Remove candidate because this exact duplicate location is unavailable and another copy was selected to keep.";
            if (preferredManagedFolderID.HasValue && !IsPreferredManagedFolder(location, preferredManagedFolderID))
                return "Remove candidate because it does not match the preferred managed folder and another exact duplicate was selected to keep.";
            if (preferredPathContains is not null && !IsPreferredPath(location, preferredPathContains))
                return "Remove candidate because it does not match the preferred path and another exact duplicate was selected to keep.";
            return "Remove candidate because another exact duplicate was selected to keep.";
        }

        var reasons = new List<string>();
        if (location.IsAvailable)
            reasons.Add("it is available on disk");
        if (IsPreferredManagedFolder(location, preferredManagedFolderID))
            reasons.Add("it matches the preferred managed folder");
        if (IsPreferredPath(location, preferredPathContains))
            reasons.Add("it matches the preferred path");
        if (video.DateTimeImported.HasValue)
            reasons.Add("it has an import timestamp");

        return reasons.Count == 0
            ? "Keep candidate selected by stable path and location ordering."
            : $"Keep candidate because {string.Join(", ", reasons)}.";
    }

    private static bool IsPreferredManagedFolder(VideoLocal_Place location, int? preferredManagedFolderID) =>
        preferredManagedFolderID.HasValue && location.ManagedFolderID == preferredManagedFolderID.Value;

    private static bool IsPreferredPath(VideoLocal_Place location, string? preferredPathContains) =>
        preferredPathContains is not null &&
        location.RelativePath.Contains(preferredPathContains, StringComparison.OrdinalIgnoreCase);

    private sealed record ExactDuplicateKey(string Hash, long FileSize);
}
