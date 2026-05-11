using System;
using DaCollector.Server.API.v3.Models.Media;
using DaCollector.Server.Media;
using DaCollector.Server.Models.TVDB;
using Xunit;

namespace DaCollector.Tests;

public class MediaDtoTests
{
    [Fact]
    public void FromTvdbShow_MapsProviderNeutralShowFields()
    {
        var show = new TVDB_Show(81189)
        {
            Name = "Breaking Bad",
            Overview = "A chemistry teacher becomes a criminal.",
            FirstAiredAt = new DateOnly(2008, 1, 20),
            LastAiredAt = new DateOnly(2013, 9, 29),
            Status = "Ended",
            Network = "AMC",
            SeasonCount = 5,
            EpisodeCount = 62,
            Genres = ["Drama"],
            Year = 2008,
            PosterPath = "poster.jpg",
        };

        var dto = MediaShowDto.FromTvdbShow(show);

        Assert.Equal("tvdb", dto.Provider);
        Assert.Equal(81189, dto.ProviderID);
        Assert.Equal("Breaking Bad", dto.Title);
        Assert.Equal(2008, dto.Year);
        Assert.Equal(5, dto.SeasonCount);
        Assert.Equal(62, dto.EpisodeCount);
        Assert.Equal("TVDB", Assert.Single(dto.ExternalIDs).Source);
    }

    [Fact]
    public void FromTvdbEpisode_MapsProviderNeutralEpisodeFields()
    {
        var episode = new TVDB_Episode(349232, 81189)
        {
            TvdbShowID = 81189,
            TvdbSeasonID = 30272,
            SeasonNumber = 1,
            EpisodeNumber = 1,
            Name = "Pilot",
            Overview = "The first episode.",
            RuntimeMinutes = 58,
            AiredAt = new DateOnly(2008, 1, 20),
        };

        var dto = MediaEpisodeDto.FromTvdbEpisode(episode);

        Assert.Equal("tvdb", dto.Provider);
        Assert.Equal(349232, dto.ProviderID);
        Assert.Equal(81189, dto.ShowProviderID);
        Assert.Equal(1, dto.SeasonNumber);
        Assert.Equal(1, dto.EpisodeNumber);
        Assert.Equal("Pilot", dto.Title);
        Assert.Equal(58, dto.RuntimeMinutes);
    }

    [Fact]
    public void IsValidProvider_RespectsAllOnlyWhenAllowed()
    {
        Assert.True(MediaReadService.IsValidProvider("tmdb", allowAll: false));
        Assert.True(MediaReadService.IsValidProvider("tvdb", allowAll: false));
        Assert.False(MediaReadService.IsValidProvider("all", allowAll: false));
        Assert.True(MediaReadService.IsValidProvider("all", allowAll: true));
        Assert.False(MediaReadService.IsValidProvider("plex", allowAll: true));
    }
}
