using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DaCollector.Abstractions.Collections;
using DaCollector.Abstractions.Metadata;
using DaCollector.Abstractions.Metadata.Enums;
using DaCollector.Server.Providers.TMDB;
using DaCollector.Server.Repositories;
using TMDbLib.Objects.Exceptions;

#nullable enable
namespace DaCollector.Server.Collections;

/// <summary>
/// Previews collection builder output from local provider data and live provider lookups.
/// </summary>
public class CollectionBuilderPreviewService(
    ITmdbCollectionBuilderClient tmdbClient,
    ITvdbCollectionBuilderClient tvdbClient
)
{
    private static readonly string[] ValueOptionKeys =
    [
        "id",
        "ids",
        "value",
        "values",
    ];

    public async Task<CollectionBuilderPreview> Preview(CollectionRule rule, CancellationToken cancellationToken = default)
    {
        if (!CollectionBuilderCatalog.TryGet(rule.Builder, out var builder))
            throw new ArgumentException($"Unknown collection builder '{rule.Builder}'.", nameof(rule));

        var warnings = new List<string>();
        var items = builder.Name.ToLowerInvariant() switch
        {
            "tmdb_movie" => await PreviewTmdbMovies(rule, warnings, cancellationToken).ConfigureAwait(false),
            "tmdb_show" => await PreviewTmdbShows(rule, warnings, cancellationToken).ConfigureAwait(false),
            "tmdb_collection" => await PreviewTmdbCollections(rule, warnings, cancellationToken).ConfigureAwait(false),
            "tmdb_popular" => await PreviewTmdbList(rule, warnings, TmdbMovieBuilderList.Popular, TmdbShowBuilderList.Popular, cancellationToken).ConfigureAwait(false),
            "tmdb_top_rated" => await PreviewTmdbList(rule, warnings, TmdbMovieBuilderList.TopRated, TmdbShowBuilderList.TopRated, cancellationToken).ConfigureAwait(false),
            "tmdb_trending_daily" => await PreviewTmdbList(rule, warnings, TmdbMovieBuilderList.TrendingDay, TmdbShowBuilderList.TrendingDay, cancellationToken).ConfigureAwait(false),
            "tmdb_trending_weekly" => await PreviewTmdbList(rule, warnings, TmdbMovieBuilderList.TrendingWeek, TmdbShowBuilderList.TrendingWeek, cancellationToken).ConfigureAwait(false),
            "tmdb_now_playing" => await PreviewTmdbMovieList(rule, warnings, TmdbMovieBuilderList.NowPlaying, cancellationToken).ConfigureAwait(false),
            "tmdb_upcoming" => await PreviewTmdbMovieList(rule, warnings, TmdbMovieBuilderList.Upcoming, cancellationToken).ConfigureAwait(false),
            "tmdb_airing_today" => await PreviewTmdbShowList(rule, warnings, TmdbShowBuilderList.AiringToday, cancellationToken).ConfigureAwait(false),
            "tmdb_on_the_air" => await PreviewTmdbShowList(rule, warnings, TmdbShowBuilderList.OnTheAir, cancellationToken).ConfigureAwait(false),
            "tmdb_discover" => await PreviewTmdbDiscover(rule, warnings, cancellationToken).ConfigureAwait(false),
            "tvdb_movie" => await PreviewTvdbMovies(rule, warnings, cancellationToken).ConfigureAwait(false),
            "tvdb_show" => await PreviewTvdbShows(rule, warnings, cancellationToken).ConfigureAwait(false),
            "tvdb_list" => await PreviewTvdbList(rule, warnings, cancellationToken).ConfigureAwait(false),
            _ => UnsupportedPreview(builder, warnings),
        };

        return new()
        {
            Builder = builder,
            Items = Deduplicate(items),
            Warnings = warnings,
        };
    }

    private async Task<IReadOnlyList<CollectionBuilderPreviewItem>> PreviewTmdbMovies(CollectionRule rule, List<string> warnings, CancellationToken cancellationToken)
    {
        var items = new List<CollectionBuilderPreviewItem>();
        foreach (var id in GetPositiveIntegerValues(rule, warnings))
        {
            var movie = TryGetCached(() => RepoFactory.TMDB_Movie.GetByTmdbMovieID(id));
            if (movie is not null)
            {
                items.Add(new()
                {
                    ExternalID = ExternalMediaId.TmdbMovie(id),
                    Title = movie.EnglishTitle,
                    Summary = movie.EnglishOverview,
                });
                continue;
            }

            try
            {
                var remoteMovie = await tmdbClient.GetMovie(id, cancellationToken).ConfigureAwait(false);
                if (remoteMovie is null)
                {
                    warnings.Add($"TMDB movie {id} was not found.");
                    continue;
                }

                items.Add(ToPreviewItem(remoteMovie));
            }
            catch (Exception e) when (IsTmdbPreviewFailure(e))
            {
                warnings.Add($"TMDB movie {id} could not be fetched: {e.Message}");
            }
        }

        return items;
    }

    private async Task<IReadOnlyList<CollectionBuilderPreviewItem>> PreviewTmdbShows(CollectionRule rule, List<string> warnings, CancellationToken cancellationToken)
    {
        var items = new List<CollectionBuilderPreviewItem>();
        foreach (var id in GetPositiveIntegerValues(rule, warnings))
        {
            var show = TryGetCached(() => RepoFactory.TMDB_Show.GetByTmdbShowID(id));
            if (show is not null)
            {
                items.Add(new()
                {
                    ExternalID = ExternalMediaId.TmdbShow(id),
                    Title = show.EnglishTitle,
                    Summary = show.EnglishOverview,
                });
                continue;
            }

            try
            {
                var remoteShow = await tmdbClient.GetShow(id, cancellationToken).ConfigureAwait(false);
                if (remoteShow is null)
                {
                    warnings.Add($"TMDB show {id} was not found.");
                    continue;
                }

                items.Add(ToPreviewItem(remoteShow));
            }
            catch (Exception e) when (IsTmdbPreviewFailure(e))
            {
                warnings.Add($"TMDB show {id} could not be fetched: {e.Message}");
            }
        }

        return items;
    }

    private async Task<IReadOnlyList<CollectionBuilderPreviewItem>> PreviewTmdbCollections(CollectionRule rule, List<string> warnings, CancellationToken cancellationToken)
    {
        var items = new List<CollectionBuilderPreviewItem>();
        var query = GetTmdbQuery(rule, warnings);
        foreach (var id in GetPositiveIntegerValues(rule, warnings))
        {
            var collection = TryGetCached(() => RepoFactory.TMDB_Collection.GetByTmdbCollectionID(id));
            if (collection is not null)
            {
                items.AddRange(collection
                    .GetTmdbMovies()
                    .Select(movie => new CollectionBuilderPreviewItem
                    {
                        ExternalID = ExternalMediaId.TmdbMovie(movie.TmdbMovieID),
                        Title = movie.EnglishTitle,
                        Summary = movie.EnglishOverview,
                    }));
                continue;
            }

            try
            {
                var remoteMovies = await tmdbClient.GetCollectionMovies(id, query, cancellationToken).ConfigureAwait(false);
                if (remoteMovies.Count == 0)
                {
                    warnings.Add($"TMDB collection {id} was not found or has no movies.");
                    continue;
                }

                items.AddRange(remoteMovies.Select(ToPreviewItem));
            }
            catch (Exception e) when (IsTmdbPreviewFailure(e))
            {
                warnings.Add($"TMDB collection {id} could not be fetched: {e.Message}");
            }
        }

        return items;
    }

    private async Task<IReadOnlyList<CollectionBuilderPreviewItem>> PreviewTmdbList(
        CollectionRule rule,
        List<string> warnings,
        TmdbMovieBuilderList movieList,
        TmdbShowBuilderList showList,
        CancellationToken cancellationToken
    )
    {
        var items = new List<CollectionBuilderPreviewItem>();
        var query = GetTmdbQuery(rule, warnings);
        var kinds = GetRequestedKinds(rule, warnings, allowMovie: true, allowShow: true);

        if (kinds.Contains(MediaKind.Movie))
            items.AddRange(await FetchTmdbMovies(query, warnings, q => tmdbClient.GetMovies(movieList, q, cancellationToken)).ConfigureAwait(false));
        if (kinds.Contains(MediaKind.Show))
            items.AddRange(await FetchTmdbShows(query, warnings, q => tmdbClient.GetShows(showList, q, cancellationToken)).ConfigureAwait(false));

        return items;
    }

    private async Task<IReadOnlyList<CollectionBuilderPreviewItem>> PreviewTmdbMovieList(
        CollectionRule rule,
        List<string> warnings,
        TmdbMovieBuilderList movieList,
        CancellationToken cancellationToken
    )
    {
        var kinds = GetRequestedKinds(rule, warnings, allowMovie: true, allowShow: false);
        if (!kinds.Contains(MediaKind.Movie))
            return [];

        var query = GetTmdbQuery(rule, warnings);
        return await FetchTmdbMovies(query, warnings, q => tmdbClient.GetMovies(movieList, q, cancellationToken)).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<CollectionBuilderPreviewItem>> PreviewTmdbShowList(
        CollectionRule rule,
        List<string> warnings,
        TmdbShowBuilderList showList,
        CancellationToken cancellationToken
    )
    {
        var kinds = GetRequestedKinds(rule, warnings, allowMovie: false, allowShow: true);
        if (!kinds.Contains(MediaKind.Show))
            return [];

        var query = GetTmdbQuery(rule, warnings);
        return await FetchTmdbShows(query, warnings, q => tmdbClient.GetShows(showList, q, cancellationToken)).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<CollectionBuilderPreviewItem>> PreviewTmdbDiscover(CollectionRule rule, List<string> warnings, CancellationToken cancellationToken)
    {
        var items = new List<CollectionBuilderPreviewItem>();
        var query = GetTmdbQuery(rule, warnings);
        var kinds = GetRequestedKinds(rule, warnings, allowMovie: true, allowShow: true);

        if (kinds.Contains(MediaKind.Movie))
            items.AddRange(await FetchTmdbMovies(query, warnings, q => tmdbClient.DiscoverMovies(q, cancellationToken)).ConfigureAwait(false));
        if (kinds.Contains(MediaKind.Show))
            items.AddRange(await FetchTmdbShows(query, warnings, q => tmdbClient.DiscoverShows(q, cancellationToken)).ConfigureAwait(false));

        return items;
    }

    private static async Task<IReadOnlyList<CollectionBuilderPreviewItem>> FetchTmdbMovies(
        TmdbBuilderQuery query,
        List<string> warnings,
        Func<TmdbBuilderQuery, Task<IReadOnlyList<TmdbBuilderMovie>>> fetch
    )
    {
        try
        {
            return (await fetch(query).ConfigureAwait(false))
                .Select(ToPreviewItem)
                .ToList();
        }
        catch (Exception e) when (IsTmdbPreviewFailure(e))
        {
            warnings.Add($"TMDB movies could not be fetched: {e.Message}");
            return [];
        }
    }

    private static async Task<IReadOnlyList<CollectionBuilderPreviewItem>> FetchTmdbShows(
        TmdbBuilderQuery query,
        List<string> warnings,
        Func<TmdbBuilderQuery, Task<IReadOnlyList<TmdbBuilderShow>>> fetch
    )
    {
        try
        {
            return (await fetch(query).ConfigureAwait(false))
                .Select(ToPreviewItem)
                .ToList();
        }
        catch (Exception e) when (IsTmdbPreviewFailure(e))
        {
            warnings.Add($"TMDB shows could not be fetched: {e.Message}");
            return [];
        }
    }

    private static CollectionBuilderPreviewItem ToPreviewItem(TmdbBuilderMovie movie) =>
        new()
        {
            ExternalID = ExternalMediaId.TmdbMovie(movie.Id),
            Title = movie.Title,
            Summary = movie.Overview,
        };

    private static CollectionBuilderPreviewItem ToPreviewItem(TmdbBuilderShow show) =>
        new()
        {
            ExternalID = ExternalMediaId.TmdbShow(show.Id),
            Title = show.Title,
            Summary = show.Overview,
        };

    private static CollectionBuilderPreviewItem ToPreviewItem(TvdbBuilderTitle title) =>
        new()
        {
            ExternalID = title.Kind is MediaKind.Movie ? ExternalMediaId.TvdbMovie(title.Id) : ExternalMediaId.TvdbShow(title.Id),
            Title = title.Title,
            Summary = title.Summary,
        };

    private async Task<IReadOnlyList<CollectionBuilderPreviewItem>> PreviewTvdbMovies(CollectionRule rule, List<string> warnings, CancellationToken cancellationToken)
    {
        var items = new List<CollectionBuilderPreviewItem>();
        foreach (var id in GetPositiveIntegerValues(rule, warnings))
        {
            try
            {
                var movie = await tvdbClient.GetMovie(id, cancellationToken).ConfigureAwait(false);
                if (movie is null)
                {
                    warnings.Add($"TVDB movie {id} was not found.");
                    items.Add(ToFallbackTvdbPreviewItem(id, MediaKind.Movie));
                    continue;
                }

                items.Add(ToPreviewItem(movie));
            }
            catch (Exception e) when (IsTvdbPreviewFailure(e))
            {
                warnings.Add($"TVDB movie {id} could not be fetched: {e.Message}");
                items.Add(ToFallbackTvdbPreviewItem(id, MediaKind.Movie));
            }
        }

        return items;
    }

    private async Task<IReadOnlyList<CollectionBuilderPreviewItem>> PreviewTvdbShows(CollectionRule rule, List<string> warnings, CancellationToken cancellationToken)
    {
        var items = new List<CollectionBuilderPreviewItem>();
        foreach (var id in GetPositiveIntegerValues(rule, warnings))
        {
            try
            {
                var show = await tvdbClient.GetShow(id, cancellationToken).ConfigureAwait(false);
                if (show is null)
                {
                    warnings.Add($"TVDB show {id} was not found.");
                    items.Add(ToFallbackTvdbPreviewItem(id, MediaKind.Show));
                    continue;
                }

                items.Add(ToPreviewItem(show));
            }
            catch (Exception e) when (IsTvdbPreviewFailure(e))
            {
                warnings.Add($"TVDB show {id} could not be fetched: {e.Message}");
                items.Add(ToFallbackTvdbPreviewItem(id, MediaKind.Show));
            }
        }

        return items;
    }

    private async Task<IReadOnlyList<CollectionBuilderPreviewItem>> PreviewTvdbList(CollectionRule rule, List<string> warnings, CancellationToken cancellationToken)
    {
        var query = GetTvdbQuery(rule, warnings);
        if (query.Kind is not (MediaKind.Unknown or MediaKind.Movie or MediaKind.Show))
            return [];

        var items = new List<CollectionBuilderPreviewItem>();
        foreach (var id in GetPositiveIntegerValues(rule, warnings))
        {
            try
            {
                var listItems = await tvdbClient.GetList(id, query, cancellationToken).ConfigureAwait(false);
                if (listItems.Count == 0)
                {
                    warnings.Add($"TVDB list {id} was not found or has no matching titles.");
                    continue;
                }

                items.AddRange(listItems.Select(ToPreviewItem));
            }
            catch (Exception e) when (IsTvdbPreviewFailure(e))
            {
                warnings.Add($"TVDB list {id} could not be fetched: {e.Message}");
            }
        }

        return items;
    }

    private static CollectionBuilderPreviewItem ToFallbackTvdbPreviewItem(int id, MediaKind kind) =>
        new()
        {
            ExternalID = kind is MediaKind.Movie ? ExternalMediaId.TvdbMovie(id) : ExternalMediaId.TvdbShow(id),
            Title = $"TVDB {(kind is MediaKind.Movie ? "Movie" : "Show")} {id}",
        };

    private static IReadOnlyList<CollectionBuilderPreviewItem> UnsupportedPreview(CollectionBuilderDescriptor builder, List<string> warnings)
    {
        warnings.Add($"Builder '{builder.Name}' is cataloged but is not implemented for local preview yet.");
        return [];
    }

    private static IReadOnlyList<CollectionBuilderPreviewItem> Deduplicate(IEnumerable<CollectionBuilderPreviewItem> items) =>
        items
            .GroupBy(item => item.ExternalID)
            .Select(group => group.First())
            .ToList();

    private static IReadOnlyList<int> GetPositiveIntegerValues(CollectionRule rule, List<string> warnings)
    {
        var values = new List<int>();
        foreach (var value in GetStringValues(rule, warnings))
        {
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) || id <= 0)
            {
                warnings.Add($"Ignoring invalid provider ID '{value}'.");
                continue;
            }

            values.Add(id);
        }

        return values;
    }

    private static IReadOnlyList<MediaKind> GetRequestedKinds(CollectionRule rule, List<string> warnings, bool allowMovie, bool allowShow)
    {
        var kind = rule.Kind;
        var optionKind = GetOptionValue(rule.Options, "kind") ?? GetOptionValue(rule.Options, "type") ?? GetOptionValue(rule.Options, "media_type");
        if (kind is MediaKind.Unknown && !string.IsNullOrWhiteSpace(optionKind))
            kind = ParseMediaKind(optionKind);

        if (kind is MediaKind.Unknown)
        {
            if (allowMovie && allowShow)
                return [MediaKind.Movie, MediaKind.Show];
            if (allowMovie)
                return [MediaKind.Movie];
            if (allowShow)
                return [MediaKind.Show];
        }

        if (kind is MediaKind.Movie && allowMovie)
            return [MediaKind.Movie];
        if (kind is MediaKind.Show && allowShow)
            return [MediaKind.Show];

        warnings.Add($"Builder does not support media kind '{kind}'.");
        return [];
    }

    private static MediaKind ParseMediaKind(string? value)
    {
        var normalized = (value ?? string.Empty).Trim();
        return normalized.ToLowerInvariant() switch
        {
            "movie" or "movies" => MediaKind.Movie,
            "show" or "shows" or "tv" or "series" => MediaKind.Show,
            _ when Enum.TryParse<MediaKind>(normalized, ignoreCase: true, out var kind) => kind,
            _ => MediaKind.Unknown,
        };
    }

    private static TmdbBuilderQuery GetTmdbQuery(CollectionRule rule, List<string> warnings) =>
        new()
        {
            Language = GetOptionValue(rule.Options, "language") ?? GetOptionValue(rule.Options, "language_code") ?? "en-US",
            Region = GetOptionValue(rule.Options, "region"),
            Timezone = GetOptionValue(rule.Options, "timezone"),
            Page = GetPositiveIntegerOption(rule, warnings, "page", 1, min: 1, max: 500),
            Limit = GetPositiveIntegerOption(rule, warnings, "limit", 20, min: 1, max: 100),
            Year = GetOptionalPositiveIntegerOption(rule, warnings, "year"),
            MinVoteAverage = GetOptionalDoubleOption(rule, warnings, "min_vote_average", "vote_average_gte"),
            MinVoteCount = GetOptionalPositiveIntegerOption(rule, warnings, "min_vote_count", "vote_count_gte"),
            OriginalLanguage = GetOptionValue(rule.Options, "original_language"),
            SortBy = GetOptionValue(rule.Options, "sort_by") ?? GetOptionValue(rule.Options, "sort"),
            Genres = GetIntegerListOption(rule, warnings, "genre", "genres", "with_genres"),
        };

    private static TvdbBuilderQuery GetTvdbQuery(CollectionRule rule, List<string> warnings) =>
        new()
        {
            Kind = GetTvdbKind(rule, warnings),
            Limit = GetPositiveIntegerOption(rule, warnings, "limit", 20, min: 1, max: 100),
        };

    private static MediaKind GetTvdbKind(CollectionRule rule, List<string> warnings)
    {
        var kind = rule.Kind;
        var optionKind = GetOptionValue(rule.Options, "kind") ?? GetOptionValue(rule.Options, "type") ?? GetOptionValue(rule.Options, "media_type");
        if (kind is MediaKind.Unknown && !string.IsNullOrWhiteSpace(optionKind))
            kind = ParseMediaKind(optionKind);

        if (kind is MediaKind.Unknown or MediaKind.Movie or MediaKind.Show)
            return kind;

        warnings.Add($"TVDB previews support Movie or Show kinds. Ignoring unsupported kind '{kind}'.");
        return MediaKind.Unknown;
    }

    private static int GetPositiveIntegerOption(CollectionRule rule, List<string> warnings, string key, int defaultValue, int min, int max)
    {
        var value = GetOptionValue(rule.Options, key);
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;

        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            warnings.Add($"Ignoring invalid {key} value '{value}'.");
            return defaultValue;
        }

        return Math.Clamp(parsed, min, max);
    }

    private static int? GetOptionalPositiveIntegerOption(CollectionRule rule, List<string> warnings, params string[] keys)
    {
        var value = keys
            .Select(key => GetOptionValue(rule.Options, key))
            .FirstOrDefault(option => !string.IsNullOrWhiteSpace(option));
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed > 0)
            return parsed;

        warnings.Add($"Ignoring invalid numeric value '{value}'.");
        return null;
    }

    private static double? GetOptionalDoubleOption(CollectionRule rule, List<string> warnings, params string[] keys)
    {
        var value = keys
            .Select(key => GetOptionValue(rule.Options, key))
            .FirstOrDefault(option => !string.IsNullOrWhiteSpace(option));
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            return parsed;

        warnings.Add($"Ignoring invalid decimal value '{value}'.");
        return null;
    }

    private static IReadOnlyList<int> GetIntegerListOption(CollectionRule rule, List<string> warnings, params string[] keys)
    {
        var raw = keys
            .Select(key => GetOptionValue(rule.Options, key))
            .FirstOrDefault(option => !string.IsNullOrWhiteSpace(option));
        if (string.IsNullOrWhiteSpace(raw))
            return [];

        var values = new List<int>();
        foreach (var value in SplitValues(raw))
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) && id > 0)
            {
                values.Add(id);
                continue;
            }

            warnings.Add($"Ignoring invalid numeric value '{value}'.");
        }

        return values;
    }

    private static IReadOnlyList<string> GetStringValues(CollectionRule rule, List<string> warnings)
    {
        var values = ValueOptionKeys
            .Select(key => GetOptionValue(rule.Options, key))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .SelectMany(SplitValues)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (values.Count == 0)
            warnings.Add("No provider IDs were supplied. Use one of: id, ids, value, values.");

        return values;
    }

    private static string? GetOptionValue(IReadOnlyDictionary<string, string> options, string key)
    {
        foreach (var (optionKey, value) in options)
        {
            if (string.Equals(optionKey, key, StringComparison.OrdinalIgnoreCase))
                return value;
        }

        return null;
    }

    private static IEnumerable<string> SplitValues(string? value) =>
        (value ?? string.Empty)
            .Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => item.Length > 0);

    private static T? TryGetCached<T>(Func<T?> getCached)
    {
        try
        {
            return getCached();
        }
        catch (Exception e) when (e is NullReferenceException or InvalidOperationException)
        {
            return default;
        }
    }

    private static bool IsTmdbPreviewFailure(Exception e) =>
        e is TmdbApiKeyUnavailableException
            or TMDbException
            or HttpRequestException
            or TaskCanceledException
            or InvalidOperationException;

    private static bool IsTvdbPreviewFailure(Exception e) =>
        e is HttpRequestException
            or TaskCanceledException
            or InvalidOperationException
            or FormatException
            or JsonException;
}
