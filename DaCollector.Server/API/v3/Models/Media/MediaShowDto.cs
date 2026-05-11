using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using DaCollector.Server.Models.TMDB;
using DaCollector.Server.Models.TVDB;

#nullable enable
namespace DaCollector.Server.API.v3.Models.Media;

public sealed class MediaShowDto
{
    [Required]
    public string Provider { get; init; } = string.Empty;

    [Required]
    public int ProviderID { get; init; }

    [Required]
    public string Title { get; init; } = string.Empty;

    [Required]
    public string OriginalTitle { get; init; } = string.Empty;

    public string? Overview { get; init; }

    public int? Year { get; init; }

    public DateOnly? FirstAiredAt { get; init; }

    public DateOnly? LastAiredAt { get; init; }

    public string? Status { get; init; }

    public string? Network { get; init; }

    public int? SeasonCount { get; init; }

    public int? EpisodeCount { get; init; }

    [Required]
    public IReadOnlyList<string> Genres { get; init; } = [];

    public string? PosterPath { get; init; }

    public string? BackdropPath { get; init; }

    [Required]
    public IReadOnlyList<MediaExternalIdDto> ExternalIDs { get; init; } = [];

    [Required]
    public DateTime CreatedAt { get; init; }

    [Required]
    public DateTime LastUpdatedAt { get; init; }

    public static MediaShowDto FromTmdbShow(TMDB_Show show)
    {
        var externalIds = new List<MediaExternalIdDto>
        {
            new() { Source = "TMDB", Value = show.TmdbShowID.ToString() },
        };
        if (show.TvdbShowID is > 0)
            externalIds.Add(new() { Source = "TVDB", Value = show.TvdbShowID.Value.ToString() });

        return new()
        {
            Provider = "tmdb",
            ProviderID = show.TmdbShowID,
            Title = show.EnglishTitle,
            OriginalTitle = show.OriginalTitle,
            Overview = string.IsNullOrWhiteSpace(show.EnglishOverview) ? null : show.EnglishOverview,
            Year = show.FirstAiredAt?.Year,
            FirstAiredAt = show.FirstAiredAt,
            LastAiredAt = show.LastAiredAt,
            SeasonCount = show.SeasonCount,
            EpisodeCount = show.EpisodeCount,
            Genres = show.Genres,
            PosterPath = string.IsNullOrWhiteSpace(show.PosterPath) ? null : show.PosterPath,
            BackdropPath = string.IsNullOrWhiteSpace(show.BackdropPath) ? null : show.BackdropPath,
            ExternalIDs = externalIds,
            CreatedAt = show.CreatedAt.ToUniversalTime(),
            LastUpdatedAt = show.LastUpdatedAt.ToUniversalTime(),
        };
    }

    public static MediaShowDto FromTvdbShow(TVDB_Show show) =>
        new()
        {
            Provider = "tvdb",
            ProviderID = show.TvdbShowID,
            Title = show.Name,
            OriginalTitle = show.Name,
            Overview = string.IsNullOrWhiteSpace(show.Overview) ? null : show.Overview,
            Year = show.Year ?? show.FirstAiredAt?.Year,
            FirstAiredAt = show.FirstAiredAt,
            LastAiredAt = show.LastAiredAt,
            Status = string.IsNullOrWhiteSpace(show.Status) ? null : show.Status,
            Network = string.IsNullOrWhiteSpace(show.Network) ? null : show.Network,
            SeasonCount = show.SeasonCount,
            EpisodeCount = show.EpisodeCount,
            Genres = show.Genres,
            PosterPath = string.IsNullOrWhiteSpace(show.PosterPath) ? null : show.PosterPath,
            ExternalIDs = [new() { Source = "TVDB", Value = show.TvdbShowID.ToString() }],
            CreatedAt = show.CreatedAt.ToUniversalTime(),
            LastUpdatedAt = show.LastUpdatedAt.ToUniversalTime(),
        };
}
