using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using DaCollector.Abstractions.Metadata.Enums;
using DaCollector.Abstractions.Extensions;
using DaCollector.Abstractions.User.Enums;
using DaCollector.Abstractions.User.Services;
using DaCollector.Server.Models.DaCollector;
using DaCollector.Server.Repositories;
using DaCollector.Server.Repositories.Cached;

#nullable enable
namespace DaCollector.Server.Services;

public class MediaGroupService
{
    private readonly ILogger<MediaGroupService> _logger;

    private readonly MediaGroup_UserRepository _groupUsers;

    private readonly StoredReleaseInfoRepository _storedReleaseInfo;

    private readonly MediaGroupRepository _groups;

    private readonly MediaSeries_UserRepository _seriesUsers;

    private readonly UserDataService _userDataService;

    public MediaGroupService(ILogger<MediaGroupService> logger, MediaGroup_UserRepository groupUsers, StoredReleaseInfoRepository storedReleaseInfo, MediaGroupRepository groups, MediaSeries_UserRepository seriesUsers, IUserDataService userDataService)
    {
        _groupUsers = groupUsers;
        _logger = logger;
        _storedReleaseInfo = storedReleaseInfo;
        _groups = groups;
        _seriesUsers = seriesUsers;
        _userDataService = (UserDataService)userDataService;
    }

    public void DeleteGroup(MediaGroup group, bool updateParent = true)
    {
        // delete all sub groups
        foreach (var subGroup in group.AllChildren)
        {
            DeleteGroup(subGroup, false);
        }

        _groups.Delete(group);

        // finally update stats
        if (updateParent)
        {
            UpdateStatsFromTopLevel(group.Parent?.TopLevelMediaGroup, true, true);
        }
    }

    public void SetMainSeries(MediaGroup group, MediaSeries? series)
    {
        // Set the id before potentially resetting the fields, so the getter uses
        // the new id instead of the old.
        group.DefaultMediaSeriesID = series?.MediaSeriesID;

        ValidateMainSeries(group);

        // Reset the name/description if the group is not manually named.
        var current = series ?? (group.MainAniDBAnimeID.HasValue
            ? RepoFactory.MediaSeries.GetByAnimeID(group.MainAniDBAnimeID.Value)
            : group.AllSeries.FirstOrDefault());
        if (group.IsManuallyNamed == 0 && current != null)
            group.GroupName = current!.Title;
        if (group.OverrideDescription == 0 && current != null)
            group.Description = current!.PreferredOverview?.Value ?? string.Empty;

        // Save the changes for this group only.
        group.DateTimeUpdated = DateTime.Now;
        _groups.Save(group, false);
    }

    public void ValidateMainSeries(MediaGroup group)
    {
        if (group.MainAniDBAnimeID == null && group.DefaultMediaSeriesID == null) return;
        var allSeries = group.AllSeries;

        // User overridden main series.
        if (group.DefaultMediaSeriesID.HasValue && !allSeries.Any(series => series.MediaSeriesID == group.DefaultMediaSeriesID.Value))
        {
            throw new InvalidOperationException("Cannot set default series to a series that does not exist in the group");
        }

        // Auto selected main series.
        if (group.MainAniDBAnimeID.HasValue && !allSeries.Any(series => series.AniDB_ID == group.MainAniDBAnimeID.Value))
        {
            throw new InvalidOperationException("Cannot set default series to a series that does not exist in the group");
        }
    }

    /// <summary>
    /// Rename all groups without a manually set name according to the current
    /// language preference.
    /// </summary>
    public void RenameAllGroups()
    {
        _logger.LogInformation("Starting RenameAllGroups");
        foreach (var grp in _groups.GetAll())
        {
            // Skip renaming any groups that are fully manually managed.
            if (grp.IsManuallyNamed == 1 && grp.OverrideDescription == 1)
                continue;

            var series = grp.MainSeries ?? grp.AllSeries.FirstOrDefault();
            if (series != null)
            {
                // Reset the name/description as needed.
                if (grp.IsManuallyNamed == 0)
                    grp.GroupName = series.Title;
                if (grp.OverrideDescription == 0)
                    grp.Description = series.PreferredOverview?.Value ?? string.Empty;

                // Save the changes for this group only.
                grp.DateTimeUpdated = DateTime.Now;
                _groups.Save(grp, false);
            }
        }
        _logger.LogInformation("Finished RenameAllGroups");
    }

    /// <summary>
    /// Update stats for all child groups and series
    /// This should only be called from the very top level group.
    /// </summary>
    public void UpdateStatsFromTopLevel(MediaGroup? group, bool watchedStats, bool missingEpsStats)
    {
        if (group is not { MediaGroupParentID: null })
        {
            return;
        }

        var start = DateTime.Now;
        _logger.LogInformation(
            $"Starting Updating STATS for GROUP {group.GroupName} from Top Level (recursively) - Watched Stats: {watchedStats}, Missing Episodes: {missingEpsStats}");

        // now recursively update stats for all the child groups
        // and update the stats for the groups
        foreach (var grp in group.AllChildren)
        {
            UpdateStats(grp, watchedStats, missingEpsStats);
        }

        UpdateStats(group, watchedStats, missingEpsStats);
        _logger.LogTrace($"Finished Updating STATS for GROUP {group.GroupName} from Top Level (recursively) in {(DateTime.Now - start).TotalMilliseconds}ms");
    }

    /// <summary>
    /// Update the stats for this group based on the child series
    /// Assumes that all the MediaSeries have had their stats updated already
    /// </summary>
    private void UpdateStats(MediaGroup group, bool watchedStats, bool missingEpsStats)
    {
        var start = DateTime.Now;
        _logger.LogInformation(
            $"Starting Updating STATS for GROUP {group.GroupName} - Watched Stats: {watchedStats}, Missing Episodes: {missingEpsStats}");
        var seriesList = group.AllSeries;

        // Reset the name/description for the group if needed.
        var mainSeries = group.IsManuallyNamed == 0 || group.OverrideDescription == 0 ? group.MainSeries ?? seriesList.FirstOrDefault() : null;
        if (mainSeries is not null)
        {
            if (group.IsManuallyNamed == 0)
                group.GroupName = mainSeries.Title;
            if (group.OverrideDescription == 0)
                group.Description = mainSeries.PreferredOverview?.Value ?? string.Empty;
        }

        if (missingEpsStats)
        {
            UpdateMissingEpisodeStats(group, seriesList);
        }

        if (watchedStats)
        {
            var allUsers = RepoFactory.JMMUser.GetAll();

            UpdateWatchedStats(group, seriesList, allUsers, (userRecord, _, isUpdated) =>
            {
                // Now update the stats for the groups
                _logger.LogTrace("Updating stats for {0}", ToString());
                if (isUpdated)
                    _groupUsers.Save(userRecord);
            });
        }

        group.DateTimeUpdated = DateTime.Now;
        _groups.Save(group, false);
        _logger.LogTrace($"Finished Updating STATS for GROUP {group.GroupName} in {(DateTime.Now - start).TotalMilliseconds}ms");
    }

    /// <summary>
    /// Batch updates watched/missing episode stats for the specified sequence of <see cref="MediaGroup"/>s.
    /// </summary>
    /// <remarks>
    /// NOTE: This method does NOT save the changes made to the database.
    /// NOTE 2: Assumes that all the MediaSeries have had their stats updated already.
    /// </remarks>
    /// <param name="animeGroups">The sequence of <see cref="MediaGroup"/>s whose missing episode stats are to be updated.</param>
    /// <param name="watchedStats"><c>true</c> to update watched stats; otherwise, <c>false</c>.</param>
    /// <param name="missingEpsStats"><c>true</c> to update missing episode stats; otherwise, <c>false</c>.</param>
    /// <param name="createdGroupUsers">The <see cref="ICollection{T}"/> to add any <see cref="MediaGroup_User"/> records
    /// that were created when updating watched stats.</param>
    /// <param name="updatedGroupUsers">The <see cref="ICollection{T}"/> to add any <see cref="MediaGroup_User"/> records
    /// that were modified when updating watched stats.</param>
    /// <exception cref="ArgumentNullException"><paramref name="animeGroups"/> is <c>null</c>.</exception>
    public void BatchUpdateStats(IEnumerable<MediaGroup> animeGroups, bool watchedStats = true,
        bool missingEpsStats = true,
        ICollection<MediaGroup_User>? createdGroupUsers = null,
        ICollection<MediaGroup_User>? updatedGroupUsers = null)
    {
        ArgumentNullException.ThrowIfNull(animeGroups);
        if (!watchedStats && !missingEpsStats)
            return; // Nothing to do

        var allUsers = RepoFactory.JMMUser.GetAll();
        foreach (var MediaGroup in animeGroups)
        {
            var MediaSeries = MediaGroup.AllSeries;
            if (missingEpsStats)
                UpdateMissingEpisodeStats(MediaGroup, MediaSeries);
            if (watchedStats)
            {
                UpdateWatchedStats(MediaGroup, MediaSeries, allUsers, (userRecord, isNew, isUpdated) =>
                {
                    if (isNew)
                    {
                        createdGroupUsers?.Add(userRecord);
                    }
                    else if (isUpdated)
                    {
                        updatedGroupUsers?.Add(userRecord);
                    }
                });
            }
        }
    }

    /// <summary>
    /// Updates the watched stats for the specified anime group.
    /// </summary>
    /// <param name="MediaGroup">The <see cref="MediaGroup"/> that is to have it's watched stats updated.</param>
    /// <param name="seriesList">The list of <see cref="MediaSeries"/> that belong to <paramref name="MediaGroup"/>.</param>
    /// <param name="allUsers">A sequence of all JMM users.</param>
    /// <param name="newAnimeGroupUsers">A method that will be called for each processed <see cref="MediaGroup_User"/>
    /// and whether or not the <see cref="MediaGroup_User"/> is new.</param>
    private void UpdateWatchedStats(MediaGroup MediaGroup,
        IReadOnlyList<MediaSeries> seriesList,
        IEnumerable<JMMUser> allUsers, Action<MediaGroup_User, bool, bool> newAnimeGroupUsers)
    {
        foreach (var user in allUsers)
        {
            _userDataService.UpdateWatchedStats(MediaGroup, user, seriesList, newAnimeGroupUsers);
        }
    }

    /// <summary>
    /// Updates the missing episode stats for the specified anime group.
    /// </summary>
    /// <remarks>
    /// NOTE: This method does NOT save the changes made to the database.
    /// NOTE 2: Assumes that all the MediaSeries have had their stats updated already.
    /// </remarks>
    /// <param name="MediaGroup">The <see cref="MediaGroup"/> that is to have it's missing episode stats updated.</param>
    /// <param name="seriesList">The list of <see cref="MediaSeries"/> that belong to <paramref name="MediaGroup"/>.</param>
    private static void UpdateMissingEpisodeStats(MediaGroup MediaGroup,
        IEnumerable<MediaSeries> seriesList)
    {
        var missingEpisodeCount = 0;
        var missingEpisodeCountGroups = 0;
        DateTime? latestEpisodeAirDate = null;

        seriesList.AsParallel().ForAll(series =>
        {
            Interlocked.Add(ref missingEpisodeCount, series.MissingEpisodeCount);
            Interlocked.Add(ref missingEpisodeCountGroups, series.MissingEpisodeCountGroups);

            // Now series.LatestEpisodeAirDate should never be greater than today
            if (!series.LatestEpisodeAirDate.HasValue)
            {
                return;
            }

            if (latestEpisodeAirDate == null)
            {
                latestEpisodeAirDate = series.LatestEpisodeAirDate;
            }
            else if (series.LatestEpisodeAirDate.Value > latestEpisodeAirDate.Value)
            {
                latestEpisodeAirDate = series.LatestEpisodeAirDate;
            }
        });

        MediaGroup.MissingEpisodeCount = missingEpisodeCount;
        MediaGroup.MissingEpisodeCountGroups = missingEpisodeCountGroups;
        MediaGroup.LatestEpisodeAirDate = latestEpisodeAirDate;
    }
}
