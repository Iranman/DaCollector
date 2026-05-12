using System;
using DaCollector.Server.API.v3.Controllers;
using DaCollector.Server.API.v3.Models.Collections;
using DaCollector.Server.Models.Internal;
using Xunit;

namespace DaCollector.Tests;

/// <summary>
/// Unit tests for the relay-facing endpoint models and status filter logic introduced to support
/// DaCollector-Relay: GET /api/v3/ManagedCollection/{id}/Members and
/// GET/POST /api/v3/MediaFileReview/Files/{fileID}/WatchedState.
/// </summary>
public class RelayEndpointTests
{
    // ── WatchedStateDto ──────────────────────────────────────────────────────

    [Fact]
    public void WatchedStateDto_DefaultsToUnwatched()
    {
        var dto = new WatchedStateDto();

        Assert.False(dto.IsWatched);
        Assert.Null(dto.WatchedDate);
        Assert.Equal(0, dto.WatchedCount);
        Assert.Equal(0L, dto.ResumePositionMs);
        Assert.Null(dto.LastUpdated);
    }

    [Fact]
    public void WatchedStateDto_ReflectsWatchedValues()
    {
        var watchedAt = new DateTime(2026, 5, 11, 10, 0, 0, DateTimeKind.Utc);
        var updatedAt = new DateTime(2026, 5, 11, 10, 0, 1, DateTimeKind.Utc);

        var dto = new WatchedStateDto
        {
            IsWatched        = true,
            WatchedDate      = watchedAt,
            WatchedCount     = 3,
            ResumePositionMs = 0,
            LastUpdated      = updatedAt,
        };

        Assert.True(dto.IsWatched);
        Assert.Equal(watchedAt, dto.WatchedDate);
        Assert.Equal(3, dto.WatchedCount);
        Assert.Equal(updatedAt, dto.LastUpdated);
    }

    // ── SetWatchedRequest ────────────────────────────────────────────────────

    [Fact]
    public void SetWatchedRequest_AllFieldsOptional()
    {
        var request = new SetWatchedRequest();

        Assert.Null(request.IsWatched);
        Assert.Null(request.WatchedDate);
    }

    [Fact]
    public void SetWatchedRequest_WatchedTrueWithExplicitDate()
    {
        var date = new DateTime(2026, 5, 11, 8, 0, 0, DateTimeKind.Utc);
        var request = new SetWatchedRequest { IsWatched = true, WatchedDate = date };

        Assert.True(request.IsWatched);
        Assert.Equal(date, request.WatchedDate);
    }

    // ── CollectionMemberDto ──────────────────────────────────────────────────

    [Fact]
    public void CollectionMemberDto_DefaultsToEmptyFileList()
    {
        var dto = new CollectionMemberDto();

        Assert.Empty(dto.Files);
        Assert.Equal(string.Empty, dto.Provider);
        Assert.Equal(string.Empty, dto.ProviderID);
        Assert.Equal(string.Empty, dto.Kind);
        Assert.Equal(string.Empty, dto.Title);
        Assert.Null(dto.Summary);
    }

    [Fact]
    public void CollectionMemberDto_StoresProviderFields()
    {
        var dto = new CollectionMemberDto
        {
            Provider   = "tmdb",
            ProviderID = "12345",
            Kind       = "Movie",
            Title      = "Gladiator",
            Summary    = "A general becomes a slave.",
            Files      = [],
        };

        Assert.Equal("tmdb", dto.Provider);
        Assert.Equal("12345", dto.ProviderID);
        Assert.Equal("Movie", dto.Kind);
        Assert.Equal("Gladiator", dto.Title);
        Assert.Equal("A general becomes a slave.", dto.Summary);
    }

    // ── MediaFileReviewStatus constants ──────────────────────────────────────

    [Fact]
    public void MediaFileReviewStatus_ManualMatchConstantMatchesExpectedValue()
    {
        // GetByManualMatch filters on Status == MediaFileReviewStatus.ManualMatch;
        // verify the string value is stable so the query predicate is unambiguous.
        Assert.Equal("ManualMatch", MediaFileReviewStatus.ManualMatch);
    }

    [Fact]
    public void MediaFileReviewState_ManualMatchStatusIsSetCorrectly()
    {
        var state = new MediaFileReviewState
        {
            Status          = MediaFileReviewStatus.ManualMatch,
            ManualProvider  = "tvdb",
            ManualProviderID = "81189",
        };

        Assert.Equal(MediaFileReviewStatus.ManualMatch, state.Status);
        Assert.Equal("tvdb", state.ManualProvider);
        Assert.Equal("81189", state.ManualProviderID);
    }
}
