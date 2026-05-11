using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using DaCollector.Server.Models.TMDB;
using DaCollector.Server.Models.TVDB;

#nullable enable
namespace DaCollector.Server.API.v3.Models.Media;

public sealed class MediaMovieDto
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

    public DateOnly? ReleasedAt { get; init; }

    public int? RuntimeMinutes { get; init; }

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

    public static MediaMovieDto FromTmdbMovie(TMDB_Movie movie)
    {
        var externalIds = new List<MediaExternalIdDto>
        {
            new() { Source = "TMDB", Value = movie.TmdbMovieID.ToString() },
        };
        if (!string.IsNullOrWhiteSpace(movie.ImdbMovieID) && movie.ImdbMovieID != "0")
            externalIds.Add(new() { Source = "IMDb", Value = movie.ImdbMovieID });

        return new()
        {
            Provider = "tmdb",
            ProviderID = movie.TmdbMovieID,
            Title = movie.EnglishTitle,
            OriginalTitle = movie.OriginalTitle,
            Overview = string.IsNullOrWhiteSpace(movie.EnglishOverview) ? null : movie.EnglishOverview,
            Year = movie.ReleasedAt?.Year,
            ReleasedAt = movie.ReleasedAt,
            RuntimeMinutes = movie.Runtime.HasValue ? (int)Math.Floor(movie.Runtime.Value.TotalMinutes) : null,
            Genres = movie.Genres,
            PosterPath = string.IsNullOrWhiteSpace(movie.PosterPath) ? null : movie.PosterPath,
            BackdropPath = string.IsNullOrWhiteSpace(movie.BackdropPath) ? null : movie.BackdropPath,
            ExternalIDs = externalIds,
            CreatedAt = movie.CreatedAt.ToUniversalTime(),
            LastUpdatedAt = movie.LastUpdatedAt.ToUniversalTime(),
        };
    }

    public static MediaMovieDto FromTvdbMovie(TVDB_Movie movie) =>
        new()
        {
            Provider = "tvdb",
            ProviderID = movie.TvdbMovieID,
            Title = movie.Name,
            OriginalTitle = movie.Name,
            Overview = string.IsNullOrWhiteSpace(movie.Overview) ? null : movie.Overview,
            Year = movie.Year ?? movie.ReleasedAt?.Year,
            ReleasedAt = movie.ReleasedAt,
            RuntimeMinutes = movie.RuntimeMinutes,
            Genres = movie.Genres,
            PosterPath = string.IsNullOrWhiteSpace(movie.PosterPath) ? null : movie.PosterPath,
            ExternalIDs = [new() { Source = "TVDB", Value = movie.TvdbMovieID.ToString() }],
            CreatedAt = movie.CreatedAt.ToUniversalTime(),
            LastUpdatedAt = movie.LastUpdatedAt.ToUniversalTime(),
        };
}
