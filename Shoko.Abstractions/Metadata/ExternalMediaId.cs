using System;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata;

/// <summary>
/// Stable external identity for a media item.
/// </summary>
public readonly record struct ExternalMediaId
{
    /// <summary>
    /// Metadata provider that owns the ID.
    /// </summary>
    public ExternalProvider Provider { get; }

    /// <summary>
    /// Kind of media identified by the provider ID.
    /// </summary>
    public MediaKind Kind { get; }

    /// <summary>
    /// Provider-specific ID value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Create a provider identity for the given media kind.
    /// </summary>
    public ExternalMediaId(ExternalProvider provider, MediaKind kind, string value)
    {
        if (provider is ExternalProvider.Unknown)
            throw new ArgumentException("Provider must be known.", nameof(provider));
        if (kind is MediaKind.Unknown)
            throw new ArgumentException("Media kind must be known.", nameof(kind));
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("External media ID cannot be empty.", nameof(value));

        Provider = provider;
        Kind = kind;
        Value = value.Trim();
    }

    /// <summary>
    /// Create a TMDB movie identity.
    /// </summary>
    public static ExternalMediaId TmdbMovie(int tmdbMovieID) =>
        NumericProviderId(ExternalProvider.TMDB, MediaKind.Movie, tmdbMovieID, nameof(tmdbMovieID));

    /// <summary>
    /// Create a TMDB show identity.
    /// </summary>
    public static ExternalMediaId TmdbShow(int tmdbShowID) =>
        NumericProviderId(ExternalProvider.TMDB, MediaKind.Show, tmdbShowID, nameof(tmdbShowID));

    /// <summary>
    /// Create a TMDB season identity.
    /// </summary>
    public static ExternalMediaId TmdbSeason(int tmdbSeasonID) =>
        NumericProviderId(ExternalProvider.TMDB, MediaKind.Season, tmdbSeasonID, nameof(tmdbSeasonID));

    /// <summary>
    /// Create a TMDB episode identity.
    /// </summary>
    public static ExternalMediaId TmdbEpisode(int tmdbEpisodeID) =>
        NumericProviderId(ExternalProvider.TMDB, MediaKind.Episode, tmdbEpisodeID, nameof(tmdbEpisodeID));

    /// <summary>
    /// Create an IMDb title identity.
    /// </summary>
    public static ExternalMediaId ImdbTitle(string imdbID, MediaKind kind) => new(ExternalProvider.IMDb, kind, imdbID);

    /// <summary>
    /// Create an IMDb movie identity.
    /// </summary>
    public static ExternalMediaId ImdbMovie(string imdbID) => ImdbTitle(imdbID, MediaKind.Movie);

    /// <summary>
    /// Create an IMDb show identity.
    /// </summary>
    public static ExternalMediaId ImdbShow(string imdbID) => ImdbTitle(imdbID, MediaKind.Show);

    /// <summary>
    /// Create a TVDB movie identity.
    /// </summary>
    public static ExternalMediaId TvdbMovie(int tvdbMovieID) =>
        NumericProviderId(ExternalProvider.TVDB, MediaKind.Movie, tvdbMovieID, nameof(tvdbMovieID));

    /// <summary>
    /// Create a TVDB show identity.
    /// </summary>
    public static ExternalMediaId TvdbShow(int tvdbShowID) =>
        NumericProviderId(ExternalProvider.TVDB, MediaKind.Show, tvdbShowID, nameof(tvdbShowID));

    /// <summary>
    /// Create a TVDB episode identity.
    /// </summary>
    public static ExternalMediaId TvdbEpisode(int tvdbEpisodeID) =>
        NumericProviderId(ExternalProvider.TVDB, MediaKind.Episode, tvdbEpisodeID, nameof(tvdbEpisodeID));

    /// <inheritdoc />
    public override string ToString() => $"{Provider}:{Kind}:{Value}";

    private static ExternalMediaId NumericProviderId(ExternalProvider provider, MediaKind kind, int value, string parameterName)
    {
        if (value <= 0)
            throw new ArgumentOutOfRangeException(parameterName, value, "External provider IDs must be positive.");

        return new ExternalMediaId(provider, kind, value.ToString());
    }
}
