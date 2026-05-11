using System;
using System.ComponentModel.DataAnnotations;
using DaCollector.Server.Models.TMDB;
using DaCollector.Server.Models.TVDB;

#nullable enable
namespace DaCollector.Server.API.v3.Models.Media;

public sealed class MediaSeasonDto
{
    [Required]
    public string Provider { get; init; } = string.Empty;

    [Required]
    public int ProviderID { get; init; }

    [Required]
    public int ShowProviderID { get; init; }

    [Required]
    public int SeasonNumber { get; init; }

    public string? SeasonType { get; init; }

    [Required]
    public string Title { get; init; } = string.Empty;

    public string? Overview { get; init; }

    public int EpisodeCount { get; init; }

    public int? Year { get; init; }

    public string? PosterPath { get; init; }

    [Required]
    public DateTime CreatedAt { get; init; }

    [Required]
    public DateTime LastUpdatedAt { get; init; }

    public static MediaSeasonDto FromTmdbSeason(TMDB_Season season) =>
        new()
        {
            Provider = "tmdb",
            ProviderID = season.TmdbSeasonID,
            ShowProviderID = season.TmdbShowID,
            SeasonNumber = season.SeasonNumber,
            Title = season.EnglishTitle,
            Overview = string.IsNullOrWhiteSpace(season.EnglishOverview) ? null : season.EnglishOverview,
            EpisodeCount = season.EpisodeCount,
            PosterPath = string.IsNullOrWhiteSpace(season.PosterPath) ? null : season.PosterPath,
            CreatedAt = season.CreatedAt.ToUniversalTime(),
            LastUpdatedAt = season.LastUpdatedAt.ToUniversalTime(),
        };

    public static MediaSeasonDto FromTvdbSeason(TVDB_Season season) =>
        new()
        {
            Provider = "tvdb",
            ProviderID = season.TvdbSeasonID,
            ShowProviderID = season.TvdbShowID,
            SeasonNumber = season.SeasonNumber,
            SeasonType = string.IsNullOrWhiteSpace(season.SeasonType) ? null : season.SeasonType,
            Title = season.Name,
            Overview = string.IsNullOrWhiteSpace(season.Overview) ? null : season.Overview,
            EpisodeCount = season.EpisodeCount,
            Year = season.Year,
            PosterPath = string.IsNullOrWhiteSpace(season.PosterPath) ? null : season.PosterPath,
            CreatedAt = season.CreatedAt.ToUniversalTime(),
            LastUpdatedAt = season.LastUpdatedAt.ToUniversalTime(),
        };
}
