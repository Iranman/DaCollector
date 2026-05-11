using System;
using DaCollector.Server.Models.Internal;
using DaCollector.Server.Parsing;
using Xunit;

namespace DaCollector.Tests;

public class MediaFileReviewStateTests
{
    [Fact]
    public void ApplyParsedResult_PersistsMovieParserFields()
    {
        var now = new DateTime(2026, 5, 8, 12, 0, 0);
        var state = new MediaFileReviewState();
        var result = new ParsedFilenameResult
        {
            OriginalPath = "Avatar The Way of Water (2022) {tmdb-76600}.mkv",
            FileName = "Avatar The Way of Water (2022) {tmdb-76600}.mkv",
            Kind = ParsedMediaKind.Movie,
            Title = "Avatar The Way of Water",
            Year = 2022,
            Quality = "2160p",
            Source = "UHD BluRay",
            Edition = "REMUX",
            VideoCodec = "HEVC",
            AudioCodec = "TrueHD Atmos",
            AudioChannels = "7.1",
            ExternalIds = [new ExternalIdGuess("TMDB", "76600")],
            HdrFormats = ["Dolby Vision", "HDR"],
            Warnings = ["review recommended"],
        };

        state.ApplyParsedResult(result, now);

        Assert.Equal("Movie", state.ParsedKind);
        Assert.Equal("Avatar The Way of Water", state.ParsedTitle);
        Assert.Equal(2022, state.ParsedYear);
        Assert.Equal("2160p", state.ParsedQuality);
        Assert.Equal("UHD BluRay", state.ParsedSource);
        Assert.Equal("REMUX", state.ParsedEdition);
        Assert.Equal("HEVC", state.ParsedVideoCodec);
        Assert.Equal("TrueHD Atmos", state.ParsedAudioCodec);
        Assert.Equal("7.1", state.ParsedAudioChannels);
        Assert.Equal(now, state.LastParsedAt);
        Assert.Equal(now, state.UpdatedAt);
        var externalId = Assert.Single(state.ParsedExternalIds);
        Assert.Equal("TMDB", externalId.Source);
        Assert.Equal("76600", externalId.Id);
        Assert.Collection(
            state.ParsedHdrFormats,
            format => Assert.Equal("Dolby Vision", format),
            format => Assert.Equal("HDR", format)
        );
        Assert.Equal("review recommended", Assert.Single(state.ParsedWarnings));
    }

    [Fact]
    public void ApplyParsedResult_PersistsTvEpisodeParserFields()
    {
        var now = new DateTime(2026, 5, 8, 12, 0, 0);
        var state = new MediaFileReviewState();
        var result = new ParsedFilenameResult
        {
            OriginalPath = "Show.Name.S01E01-E02.mkv",
            FileName = "Show.Name.S01E01-E02.mkv",
            Kind = ParsedMediaKind.MultiEpisodeTvFile,
            ShowTitle = "Show Name",
            SeasonNumber = 1,
            EpisodeNumbers = [1, 2],
            AirDate = new DateOnly(2024, 5, 10),
        };

        state.ApplyParsedResult(result, now);

        Assert.Equal("MultiEpisodeTvFile", state.ParsedKind);
        Assert.Equal("Show Name", state.ParsedShowTitle);
        Assert.Equal(1, state.ParsedSeasonNumber);
        Assert.Collection(
            state.ParsedEpisodeNumbers,
            episode => Assert.Equal(1, episode),
            episode => Assert.Equal(2, episode)
        );
        Assert.Equal("2024-05-10", state.ParsedAirDate);
    }
}
