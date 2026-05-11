using System;
using System.ComponentModel.DataAnnotations;
using DaCollector.Server.Models.TMDB;
using DaCollector.Server.Models.TVDB;

#nullable enable
namespace DaCollector.Server.API.v3.Models.Media;

public sealed class MediaEpisodeDto
{
    [Required]
    public string Provider { get; init; } = string.Empty;

    [Required]
    public int ProviderID { get; init; }

    [Required]
    public int ShowProviderID { get; init; }

    public int? SeasonProviderID { get; init; }

    [Required]
    public int SeasonNumber { get; init; }

    [Required]
    public int EpisodeNumber { get; init; }

    [Required]
    public string Title { get; init; } = string.Empty;

    public string? Overview { get; init; }

    public int? RuntimeMinutes { get; init; }

    public DateOnly? AiredAt { get; init; }

    public string? ThumbnailPath { get; init; }

    public bool? IsHidden { get; init; }

    [Required]
    public DateTime CreatedAt { get; init; }

    [Required]
    public DateTime LastUpdatedAt { get; init; }

    public static MediaEpisodeDto FromTmdbEpisode(TMDB_Episode episode) =>
        new()
        {
            Provider = "tmdb",
            ProviderID = episode.TmdbEpisodeID,
            ShowProviderID = episode.TmdbShowID,
            SeasonProviderID = episode.TmdbSeasonID,
            SeasonNumber = episode.SeasonNumber,
            EpisodeNumber = episode.EpisodeNumber,
            Title = episode.EnglishTitle,
            Overview = string.IsNullOrWhiteSpace(episode.EnglishOverview) ? null : episode.EnglishOverview,
            RuntimeMinutes = episode.RuntimeMinutes,
            AiredAt = episode.AiredAt,
            ThumbnailPath = string.IsNullOrWhiteSpace(episode.ThumbnailPath) ? null : episode.ThumbnailPath,
            IsHidden = episode.IsHidden,
            CreatedAt = episode.CreatedAt.ToUniversalTime(),
            LastUpdatedAt = episode.LastUpdatedAt.ToUniversalTime(),
        };

    public static MediaEpisodeDto FromTvdbEpisode(TVDB_Episode episode) =>
        new()
        {
            Provider = "tvdb",
            ProviderID = episode.TvdbEpisodeID,
            ShowProviderID = episode.TvdbShowID,
            SeasonProviderID = episode.TvdbSeasonID,
            SeasonNumber = episode.SeasonNumber,
            EpisodeNumber = episode.EpisodeNumber,
            Title = episode.Name,
            Overview = string.IsNullOrWhiteSpace(episode.Overview) ? null : episode.Overview,
            RuntimeMinutes = episode.RuntimeMinutes,
            AiredAt = episode.AiredAt,
            CreatedAt = episode.CreatedAt.ToUniversalTime(),
            LastUpdatedAt = episode.LastUpdatedAt.ToUniversalTime(),
        };
}
