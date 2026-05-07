using System;
using System.Linq;
using DaCollector.Abstractions.Duplicates;
using DaCollector.Abstractions.MediaServers.Plex;
using DaCollector.Abstractions.Metadata;
using DaCollector.Server.Duplicates;
using Xunit;

#nullable enable
namespace DaCollector.Tests;

public class MediaDuplicateReviewServiceTests
{
    [Fact]
    public void FindDuplicateMediaEntries_GroupsPlexItemsByProviderIdAndTitleYear()
    {
        var duplicates = MediaDuplicateReviewService.FindDuplicateMediaEntries(
        [
            new()
            {
                RatingKey = "100",
                Title = "Star Wars",
                Type = "movie",
                Year = 1977,
                ExternalIDs = [ExternalMediaId.TmdbMovie(11)],
                GuidValues = ["tmdb://11"],
                FilePaths = ["D:\\Media\\Movies\\Star Wars.mkv"],
            },
            new()
            {
                RatingKey = "101",
                Title = "Star Wars",
                Type = "movie",
                Year = 1977,
                ExternalIDs = [ExternalMediaId.TmdbMovie(11)],
                GuidValues = ["tmdb://11"],
                FilePaths = ["D:\\Media\\Movies\\Star Wars - duplicate.mkv"],
            },
        ]);

        var set = Assert.Single(duplicates);
        Assert.Equal(MediaDuplicateMatchType.ProviderID, set.PrimaryMatchType);
        Assert.Equal(100, set.Score);
        Assert.False(set.SafeDeleteCandidate);
        Assert.Equal(2, set.CandidateCount);
        Assert.Contains(set.ScoringReasons, reason => reason.Contains("Provider ID match", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(set.ScoringReasons, reason => reason.Contains("Title/year match", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(set.ScoringReasons, reason => reason.Contains("Plex rating keys", StringComparison.OrdinalIgnoreCase));
        Assert.All(set.Items, item => Assert.Single(item.PathHashes));
    }

    [Fact]
    public void FindDuplicateMediaEntries_MarksSafeDeleteWhenAllFilesAreCoveredByOtherEntry()
    {
        // Item 200 has 2 files that also appear in item 201 → 200 is a safe-delete candidate.
        // Item 201 has one additional unique file → it is NOT a safe-delete candidate.
        var duplicates = MediaDuplicateReviewService.FindDuplicateMediaEntries(
        [
            new()
            {
                RatingKey = "200",
                Title = "The Movie",
                Type = "movie",
                Year = 2020,
                ExternalIDs = [ExternalMediaId.TmdbMovie(42)],
                GuidValues = ["tmdb://42"],
                FilePaths = ["D:\\Media\\The Movie.mkv", "D:\\Media\\The Movie - extra.mkv"],
            },
            new()
            {
                RatingKey = "201",
                Title = "The Movie",
                Type = "movie",
                Year = 2020,
                ExternalIDs = [ExternalMediaId.TmdbMovie(42)],
                GuidValues = ["tmdb://42"],
                FilePaths = ["D:\\Media\\The Movie.mkv", "D:\\Media\\The Movie - extra.mkv", "D:\\Media\\The Movie - hd.mkv"],
            },
        ]);

        var set = Assert.Single(duplicates);
        Assert.True(set.SafeDeleteCandidate);
        Assert.Contains("safe to remove", set.ReviewAction, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FindDuplicateMediaEntries_DoesNotMarkSafeDeleteWhenBothEntriesHaveUniqueFiles()
    {
        var duplicates = MediaDuplicateReviewService.FindDuplicateMediaEntries(
        [
            new()
            {
                RatingKey = "300",
                Title = "Another Movie",
                Type = "movie",
                Year = 2021,
                ExternalIDs = [ExternalMediaId.TmdbMovie(99)],
                GuidValues = ["tmdb://99"],
                FilePaths = ["D:\\Media\\Another Movie - v1.mkv"],
            },
            new()
            {
                RatingKey = "301",
                Title = "Another Movie",
                Type = "movie",
                Year = 2021,
                ExternalIDs = [ExternalMediaId.TmdbMovie(99)],
                GuidValues = ["tmdb://99"],
                FilePaths = ["D:\\Media\\Another Movie - v2.mkv"],
            },
        ]);

        var set = Assert.Single(duplicates);
        Assert.False(set.SafeDeleteCandidate);
        Assert.Contains("Review in Plex", set.ReviewAction, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FindDuplicateMediaEntries_UsesPathHashForSameFileAcrossDifferentPlexEntries()
    {
        var duplicates = MediaDuplicateReviewService.FindDuplicateMediaEntries(
        [
            new()
            {
                RatingKey = "200",
                Title = "Movie A",
                Type = "movie",
                Year = 2001,
                FilePaths = ["D:\\Media\\Movies\\Same File.mkv"],
            },
            new()
            {
                RatingKey = "201",
                Title = "Movie B",
                Type = "movie",
                Year = 2002,
                FilePaths = ["d:/media/movies/same file.mkv"],
            },
        ]);

        var set = Assert.Single(duplicates);
        Assert.Equal(MediaDuplicateMatchType.PathHash, set.PrimaryMatchType);
        Assert.Equal(95, set.Score);
        Assert.Contains(set.ScoringReasons, reason => reason.Contains("Path hash match", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, set.Items.SelectMany(item => item.PathHashes).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }
}
