using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using DaCollector.Abstractions.Metadata;
using DaCollector.Abstractions.Metadata.Enums;
using DaCollector.Server.API.v3.Models.Common;
using DaCollector.Server.Models.TMDB;

#nullable enable
namespace DaCollector.Server.API.v3.Models.MediaCatalog;

/// <summary>
/// Generic movie or TV show item for DaCollector catalog workflows.
/// </summary>
public class MediaCatalogItem
{
    /// <summary>
    /// Movie or TV show.
    /// </summary>
    [Required]
    [JsonConverter(typeof(StringEnumConverter))]
    public MediaKind Kind { get; init; }

    /// <summary>
    /// Primary external identity for this item.
    /// </summary>
    [Required]
    public MediaCatalogExternalId ExternalID { get; init; } = new();

    /// <summary>
    /// All known external identities for this item.
    /// </summary>
    [Required]
    public IReadOnlyList<MediaCatalogExternalId> ExternalIDs { get; init; } = [];

    /// <summary>
    /// Display title.
    /// </summary>
    [Required]
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Original title from the metadata provider.
    /// </summary>
    [Required]
    public string OriginalTitle { get; init; } = string.Empty;

    /// <summary>
    /// Provider overview or plot summary.
    /// </summary>
    public string? Overview { get; init; }

    /// <summary>
    /// Original language code.
    /// </summary>
    [Required]
    public string OriginalLanguage { get; init; } = string.Empty;

    /// <summary>
    /// First known release or air date.
    /// </summary>
    public DateOnly? ReleasedAt { get; init; }

    /// <summary>
    /// Year derived from the first known release or air date.
    /// </summary>
    public int? Year { get; init; }

    /// <summary>
    /// Last known air date for TV shows.
    /// </summary>
    public DateOnly? LastAiredAt { get; init; }

    /// <summary>
    /// Movie runtime, if known.
    /// </summary>
    public TimeSpan? Runtime { get; init; }

    /// <summary>
    /// Episode count for TV shows.
    /// </summary>
    public int? EpisodeCount { get; init; }

    /// <summary>
    /// Season count for TV shows.
    /// </summary>
    public int? SeasonCount { get; init; }

    /// <summary>
    /// Genres from the metadata provider.
    /// </summary>
    [Required]
    public IReadOnlyList<string> Genres { get; init; } = [];

    /// <summary>
    /// User rating from the metadata provider.
    /// </summary>
    [Required]
    public Rating UserRating { get; init; } = new();

    /// <summary>
    /// Provider poster path, if known.
    /// </summary>
    public string? PosterPath { get; init; }

    /// <summary>
    /// Provider backdrop path, if known.
    /// </summary>
    public string? BackdropPath { get; init; }

    /// <summary>
    /// Indicates the item is marked restricted by the metadata provider.
    /// </summary>
    [Required]
    public bool IsRestricted { get; init; }

    /// <summary>
    /// Indicates a TMDB movie entry is not truly a movie.
    /// </summary>
    public bool? IsVideo { get; init; }

    /// <summary>
    /// When the local metadata was first created.
    /// </summary>
    [Required]
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// When the local metadata was last updated from the provider.
    /// </summary>
    [Required]
    public DateTime LastUpdatedAt { get; init; }

    public static MediaCatalogItem FromMovie(TMDB_Movie movie)
    {
        var externalIDs = new List<ExternalMediaId> { ExternalMediaId.TmdbMovie(movie.TmdbMovieID) };
        var imdbID = movie.ImdbMovieID?.Trim();
        if (!string.IsNullOrWhiteSpace(imdbID) && !string.Equals(imdbID, "0", StringComparison.Ordinal))
            externalIDs.Add(ExternalMediaId.ImdbMovie(imdbID));

        return new()
        {
            Kind = MediaKind.Movie,
            ExternalID = new(externalIDs[0]),
            ExternalIDs = externalIDs.Select(id => new MediaCatalogExternalId(id)).ToList(),
            Title = movie.EnglishTitle,
            OriginalTitle = movie.OriginalTitle,
            Overview = string.IsNullOrWhiteSpace(movie.EnglishOverview) ? null : movie.EnglishOverview,
            OriginalLanguage = movie.OriginalLanguageCode,
            ReleasedAt = movie.ReleasedAt,
            Year = movie.ReleasedAt?.Year,
            Runtime = movie.Runtime,
            Genres = movie.Genres,
            UserRating = CreateTmdbRating(movie.UserRating, movie.UserVotes),
            PosterPath = string.IsNullOrWhiteSpace(movie.PosterPath) ? null : movie.PosterPath,
            BackdropPath = string.IsNullOrWhiteSpace(movie.BackdropPath) ? null : movie.BackdropPath,
            IsRestricted = movie.IsRestricted,
            IsVideo = movie.IsVideo,
            CreatedAt = movie.CreatedAt.ToUniversalTime(),
            LastUpdatedAt = movie.LastUpdatedAt.ToUniversalTime(),
        };
    }

    public static MediaCatalogItem FromShow(TMDB_Show show)
    {
        var externalIDs = new List<ExternalMediaId> { ExternalMediaId.TmdbShow(show.TmdbShowID) };
        if (show.TvdbShowID is > 0)
            externalIDs.Add(ExternalMediaId.TvdbShow(show.TvdbShowID.Value));

        return new()
        {
            Kind = MediaKind.Show,
            ExternalID = new(externalIDs[0]),
            ExternalIDs = externalIDs.Select(id => new MediaCatalogExternalId(id)).ToList(),
            Title = show.EnglishTitle,
            OriginalTitle = show.OriginalTitle,
            Overview = string.IsNullOrWhiteSpace(show.EnglishOverview) ? null : show.EnglishOverview,
            OriginalLanguage = show.OriginalLanguageCode,
            ReleasedAt = show.FirstAiredAt,
            Year = show.FirstAiredAt?.Year,
            LastAiredAt = show.LastAiredAt,
            EpisodeCount = show.EpisodeCount,
            SeasonCount = show.SeasonCount,
            Genres = show.Genres,
            UserRating = CreateTmdbRating(show.UserRating, show.UserVotes),
            PosterPath = string.IsNullOrWhiteSpace(show.PosterPath) ? null : show.PosterPath,
            BackdropPath = string.IsNullOrWhiteSpace(show.BackdropPath) ? null : show.BackdropPath,
            IsRestricted = show.IsRestricted,
            CreatedAt = show.CreatedAt.ToUniversalTime(),
            LastUpdatedAt = show.LastUpdatedAt.ToUniversalTime(),
        };
    }

    private static Rating CreateTmdbRating(double value, int votes) =>
        new()
        {
            Value = value,
            MaxValue = 10,
            Votes = votes,
            Source = "TMDB",
            Type = "User",
        };
}
