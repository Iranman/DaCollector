using System;
using DaCollector.Server.Parsing;
using Xunit;

namespace DaCollector.Tests;

public class FilenameParserServiceTests
{
    private readonly FilenameParserService _parser = new();

    [Fact]
    public void Parse_DetectsMovieTitleYearAndQuality()
    {
        var result = _parser.Parse("The.Matrix.1999.2160p.UHD.BluRay.REMUX.DV.HDR.HEVC.TrueHD.7.1.Atmos.mkv");

        Assert.Equal(ParsedMediaKind.Movie, result.Kind);
        Assert.Equal("The Matrix", result.Title);
        Assert.Equal(1999, result.Year);
        Assert.Equal("2160p", result.Quality);
        Assert.Equal("UHD BluRay", result.Source);
        Assert.Equal("REMUX", result.Edition);
        Assert.Equal("HEVC", result.VideoCodec);
        Assert.Equal("TrueHD Atmos", result.AudioCodec);
        Assert.Equal("7.1", result.AudioChannels);
        Assert.Collection(
            result.HdrFormats,
            format => Assert.Equal("Dolby Vision", format),
            format => Assert.Equal("HDR", format)
        );
    }

    [Fact]
    public void Parse_DetectsExplicitTmdbId()
    {
        var result = _parser.Parse("Avatar The Way of Water (2022) {tmdb-76600}.mkv");

        Assert.Equal(ParsedMediaKind.Movie, result.Kind);
        Assert.Equal("Avatar The Way of Water", result.Title);
        Assert.Equal(2022, result.Year);
        var id = Assert.Single(result.ExternalIds);
        Assert.Equal("TMDB", id.Source);
        Assert.Equal("76600", id.Id);
    }

    [Fact]
    public void Parse_DetectsTvEpisode()
    {
        var result = _parser.Parse("Breaking.Bad.S01E01.1080p.BluRay.x265.mkv");

        Assert.Equal(ParsedMediaKind.TvEpisode, result.Kind);
        Assert.Equal("Breaking Bad", result.ShowTitle);
        Assert.Equal(1, result.SeasonNumber);
        var episode = Assert.Single(result.EpisodeNumbers);
        Assert.Equal(1, episode);
        Assert.Equal("1080p", result.Quality);
        Assert.Equal("BluRay", result.Source);
        Assert.Equal("HEVC", result.VideoCodec);
    }

    [Fact]
    public void Parse_DetectsDateBasedTvEpisode()
    {
        var result = _parser.Parse("The Daily Show 2024-05-10.mkv");

        Assert.Equal(ParsedMediaKind.TvEpisode, result.Kind);
        Assert.Equal("The Daily Show", result.ShowTitle);
        Assert.Equal(new DateOnly(2024, 5, 10), result.AirDate);
    }

    [Fact]
    public void Parse_DetectsMultiEpisodeTvFile()
    {
        var result = _parser.Parse("Show.Name.S01E01-E02.mkv");

        Assert.Equal(ParsedMediaKind.MultiEpisodeTvFile, result.Kind);
        Assert.Equal("Show Name", result.ShowTitle);
        Assert.Equal(1, result.SeasonNumber);
        Assert.Collection(
            result.EpisodeNumbers,
            episode => Assert.Equal(1, episode),
            episode => Assert.Equal(2, episode)
        );
    }
}
