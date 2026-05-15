using System;
using System.Collections.Generic;
using DaCollector.Server.API.v3.Controllers;
using DaCollector.Server.Media;
using Xunit;

namespace DaCollector.Tests;

public class MissingFileReviewTests
{
    [Fact]
    public void MissingFileItem_RoundTrips_AllFields()
    {
        var location = new MediaFileReviewLocation
        {
            ID = 1,
            ManagedFolderID = 5,
            RelativePath = "Movies/Gone Girl (2014).mkv",
            Path = "/mnt/media/Movies/Gone Girl (2014).mkv",
            FileName = "Gone Girl (2014).mkv",
            IsAvailable = false,
        };
        var link = new MissingFileLink
        {
            Provider = "TMDB",
            EntityType = "Movie",
            ProviderID = "76341",
            Title = "Gone Girl",
        };
        var imported = new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);

        var item = new MissingFileItem
        {
            FileID = 42,
            Hash = "abc123",
            FileSize = 1_000_000_000L,
            DateImported = imported,
            Locations = [location],
            LinkedTo = [link],
        };

        Assert.Equal(42, item.FileID);
        Assert.Equal("abc123", item.Hash);
        Assert.Equal(1_000_000_000L, item.FileSize);
        Assert.Equal(imported, item.DateImported);
        var loc = Assert.Single(item.Locations);
        Assert.False(loc.IsAvailable);
        Assert.Equal("Gone Girl (2014).mkv", loc.FileName);
        var l = Assert.Single(item.LinkedTo);
        Assert.Equal("TMDB", l.Provider);
        Assert.Equal("Movie", l.EntityType);
        Assert.Equal("76341", l.ProviderID);
        Assert.Equal("Gone Girl", l.Title);
    }

    [Fact]
    public void MissingFileLink_AllProviderTypes_AreValid()
    {
        var tmdbMovie  = new MissingFileLink { Provider = "TMDB",  EntityType = "Movie",   ProviderID = "1" };
        var tmdbEp     = new MissingFileLink { Provider = "TMDB",  EntityType = "Episode", ProviderID = "2" };
        var tvdbEp     = new MissingFileLink { Provider = "TVDB",  EntityType = "Episode", ProviderID = "3" };
        var local      = new MissingFileLink { Provider = "Local", EntityType = "Episode", ProviderID = "4" };

        Assert.Equal("Movie",   tmdbMovie.EntityType);
        Assert.Equal("Episode", tmdbEp.EntityType);
        Assert.Equal("TVDB",    tvdbEp.Provider);
        Assert.Equal("Local",   local.Provider);
    }

    [Fact]
    public void MissingFileLink_NullTitle_IsAllowed()
    {
        var link = new MissingFileLink { Provider = "TMDB", EntityType = "Episode", ProviderID = "9", Title = null };
        Assert.Null(link.Title);
    }

    [Fact]
    public void MissingFileItem_EmptyLinksAndLocations_IsValid()
    {
        var item = new MissingFileItem { FileID = 7, Hash = "deadbeef", FileSize = 0 };
        Assert.Empty(item.LinkedTo);
        Assert.Empty(item.Locations);
    }

    [Fact]
    public void CorruptFileItem_ZeroSize_ReasonFlagged()
    {
        var item = new CorruptFileItem
        {
            FileID = 10,
            Hash = "abc",
            FileSize = 0,
            DurationMs = 0,
            MediaInfoVersion = 5,
            DateImported = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            Reasons = ["ZeroSize", "ZeroDuration"],
            Locations = [],
        };

        Assert.Contains("ZeroSize", item.Reasons);
        Assert.Contains("ZeroDuration", item.Reasons);
        Assert.Equal(0L, item.FileSize);
        Assert.Equal(0L, item.DurationMs);
    }

    [Fact]
    public void CorruptFileItem_ZeroDurationOnly_SingleReason()
    {
        var item = new CorruptFileItem
        {
            FileID = 11,
            Hash = "def",
            FileSize = 2_000_000_000L,
            DurationMs = 0,
            MediaInfoVersion = 5,
            Reasons = ["ZeroDuration"],
            Locations = [],
        };

        Assert.Equal("ZeroDuration", Assert.Single(item.Reasons));
        Assert.NotEqual(0L, item.FileSize);
    }

    [Fact]
    public void RenamePlanItem_RoundTrips_AllFields()
    {
        var item = new RenamePlanItem
        {
            FileID = 20,
            LocationID = 5,
            ManagedFolderID = 2,
            CurrentRelativePath = "unsorted/gone.girl.2014.mkv",
            ProposedRelativePath = "Movies/Gone Girl (2014)/Gone Girl (2014) [2160p].mkv",
            WouldRename = true,
            WouldMove = true,
        };

        Assert.Equal(20, item.FileID);
        Assert.Equal(5, item.LocationID);
        Assert.True(item.WouldRename);
        Assert.True(item.WouldMove);
        Assert.NotEqual(item.CurrentRelativePath, item.ProposedRelativePath);
    }

    [Fact]
    public void RenamePlanItem_RenameOnly_MoveIsFalse()
    {
        var item = new RenamePlanItem
        {
            FileID = 21,
            LocationID = 6,
            ManagedFolderID = 2,
            CurrentRelativePath = "Movies/gone.girl.2014.mkv",
            ProposedRelativePath = "Movies/Gone Girl (2014) [2160p].mkv",
            WouldRename = true,
            WouldMove = false,
        };

        Assert.True(item.WouldRename);
        Assert.False(item.WouldMove);
    }
}
