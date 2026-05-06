using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Duplicates;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.Duplicates;

/// <summary>
/// Finds exact duplicate video locations using stored hash and file size data.
/// </summary>
public class ExactDuplicateService
{
    public IReadOnlyList<ExactDuplicateSet> GetExactDuplicates(bool includeIgnored = false, bool onlyAvailable = false)
    {
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

        return groups.Select(ToDuplicateSet).ToList();
    }

    public ExactDuplicateSummary GetSummary(bool includeIgnored = false, bool onlyAvailable = false)
    {
        var sets = GetExactDuplicates(includeIgnored, onlyAvailable);
        return new()
        {
            SetCount = sets.Count,
            LocationCount = sets.Sum(set => set.LocationCount),
            SuggestedRemoveLocationCount = sets.Sum(set => set.SuggestedRemoveLocationIDs.Count),
            PotentialReclaimBytes = sets.Sum(set => set.FileSize * set.SuggestedRemoveLocationIDs.Count),
            AvailableLocationCount = sets.Sum(set => set.AvailableLocationCount),
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

    private static ExactDuplicateSet ToDuplicateSet(IGrouping<ExactDuplicateKey, (VideoLocal video, IReadOnlyList<VideoLocal_Place> locations)> group)
    {
        var flattened = group
            .SelectMany(tuple => tuple.locations.Select(location => (tuple.video, location)))
            .OrderByDescending(tuple => tuple.location.IsAvailable)
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
            Locations = flattened.Select(tuple => ToLocation(tuple.video, tuple.location, keepLocationID)).ToList(),
        };
    }

    private static ExactDuplicateLocation ToLocation(VideoLocal video, VideoLocal_Place location, int? keepLocationID)
    {
        var managedFolder = location.ManagedFolder;
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
            SuggestedKeep = location.ID == keepLocationID,
            SuggestedRemove = location.ID != keepLocationID,
            ImportedAt = video.DateTimeImported,
            CreatedAt = video.DateTimeCreated,
        };
    }

    private sealed record ExactDuplicateKey(string Hash, long FileSize);
}
