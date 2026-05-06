using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DaCollector.Server.Providers.TMDB;
using TMDbLib.Client;
using TMDbLib.Objects.Collections;
using TMDbLib.Objects.Discover;
using TMDbLib.Objects.General;
using TMDbLib.Objects.Movies;
using TMDbLib.Objects.Search;
using TMDbLib.Objects.Trending;
using TMDbLib.Objects.TvShows;

#nullable enable
namespace DaCollector.Server.Collections;

public interface ITmdbCollectionBuilderClient
{
    Task<TmdbBuilderMovie?> GetMovie(int id, CancellationToken cancellationToken = default);

    Task<TmdbBuilderShow?> GetShow(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TmdbBuilderMovie>> GetCollectionMovies(int id, TmdbBuilderQuery query, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TmdbBuilderMovie>> GetMovies(TmdbMovieBuilderList list, TmdbBuilderQuery query, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TmdbBuilderShow>> GetShows(TmdbShowBuilderList list, TmdbBuilderQuery query, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TmdbBuilderMovie>> DiscoverMovies(TmdbBuilderQuery query, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TmdbBuilderShow>> DiscoverShows(TmdbBuilderQuery query, CancellationToken cancellationToken = default);
}

public class TmdbCollectionBuilderClient(TmdbMetadataService tmdbMetadataService) : ITmdbCollectionBuilderClient
{
    public async Task<TmdbBuilderMovie?> GetMovie(int id, CancellationToken cancellationToken = default)
    {
        var movie = await tmdbMetadataService
            .UseClient(client => client.GetMovieAsync(id, "en-US", null, MovieMethods.Undefined, cancellationToken), $"Get TMDB movie {id} for collection preview")
            .ConfigureAwait(false);

        return movie is null ? null : ToMovie(movie);
    }

    public async Task<TmdbBuilderShow?> GetShow(int id, CancellationToken cancellationToken = default)
    {
        var show = await tmdbMetadataService
            .UseClient(client => client.GetTvShowAsync(id, TvShowMethods.Undefined, "en-US", null, cancellationToken), $"Get TMDB show {id} for collection preview")
            .ConfigureAwait(false);

        return show is null ? null : ToShow(show);
    }

    public async Task<IReadOnlyList<TmdbBuilderMovie>> GetCollectionMovies(int id, TmdbBuilderQuery query, CancellationToken cancellationToken = default)
    {
        var collection = await tmdbMetadataService
            .UseClient(client => client.GetCollectionAsync(id, query.Language, null, CollectionMethods.Undefined, cancellationToken), $"Get TMDB collection {id} for collection preview")
            .ConfigureAwait(false);

        return TakeLimit(collection?.Parts?.Select(ToMovie), query.Limit);
    }

    public async Task<IReadOnlyList<TmdbBuilderMovie>> GetMovies(TmdbMovieBuilderList list, TmdbBuilderQuery query, CancellationToken cancellationToken = default)
    {
        SearchContainer<SearchMovie>? container = await tmdbMetadataService
            .UseClient(client => GetMovieList(client, list, query, cancellationToken), $"Get TMDB {list} movies for collection preview")
            .ConfigureAwait(false);

        return TakeLimit(container?.Results?.Select(ToMovie), query.Limit);
    }

    public async Task<IReadOnlyList<TmdbBuilderShow>> GetShows(TmdbShowBuilderList list, TmdbBuilderQuery query, CancellationToken cancellationToken = default)
    {
        SearchContainer<SearchTv>? container = await tmdbMetadataService
            .UseClient(client => GetShowList(client, list, query, cancellationToken), $"Get TMDB {list} shows for collection preview")
            .ConfigureAwait(false);

        return TakeLimit(container?.Results?.Select(ToShow), query.Limit);
    }

    public async Task<IReadOnlyList<TmdbBuilderMovie>> DiscoverMovies(TmdbBuilderQuery query, CancellationToken cancellationToken = default)
    {
        SearchContainer<SearchMovie>? container = await tmdbMetadataService
            .UseClient(client =>
            {
                var discover = client.DiscoverMoviesAsync();
                ApplyMovieDiscoverOptions(discover, query);
                return discover.Query(query.Language, query.Page, cancellationToken);
            }, "Discover TMDB movies for collection preview")
            .ConfigureAwait(false);

        return TakeLimit(container?.Results?.Select(ToMovie), query.Limit);
    }

    public async Task<IReadOnlyList<TmdbBuilderShow>> DiscoverShows(TmdbBuilderQuery query, CancellationToken cancellationToken = default)
    {
        SearchContainer<SearchTv>? container = await tmdbMetadataService
            .UseClient(client =>
            {
                var discover = client.DiscoverTvShowsAsync();
                ApplyShowDiscoverOptions(discover, query);
                return discover.Query(query.Language, query.Page, cancellationToken);
            }, "Discover TMDB shows for collection preview")
            .ConfigureAwait(false);

        return TakeLimit(container?.Results?.Select(ToShow), query.Limit);
    }

    private static async Task<SearchContainer<SearchMovie>?> GetMovieList(TMDbClient client, TmdbMovieBuilderList list, TmdbBuilderQuery query, CancellationToken cancellationToken) =>
        list switch
        {
            TmdbMovieBuilderList.Popular => await client.GetMoviePopularListAsync(query.Language, query.Page, query.Region, cancellationToken).ConfigureAwait(false),
            TmdbMovieBuilderList.TopRated => await client.GetMovieTopRatedListAsync(query.Language, query.Page, query.Region, cancellationToken).ConfigureAwait(false),
            TmdbMovieBuilderList.TrendingDay => await client.GetTrendingMoviesAsync(TimeWindow.Day, query.Page, query.Language, cancellationToken).ConfigureAwait(false),
            TmdbMovieBuilderList.TrendingWeek => await client.GetTrendingMoviesAsync(TimeWindow.Week, query.Page, query.Language, cancellationToken).ConfigureAwait(false),
            TmdbMovieBuilderList.NowPlaying => await client.GetMovieNowPlayingListAsync(query.Language, query.Page, query.Region, cancellationToken).ConfigureAwait(false),
            TmdbMovieBuilderList.Upcoming => await client.GetMovieUpcomingListAsync(query.Language, query.Page, query.Region, cancellationToken).ConfigureAwait(false),
            _ => null,
        };

    private static Task<SearchContainer<SearchTv>?> GetShowList(TMDbClient client, TmdbShowBuilderList list, TmdbBuilderQuery query, CancellationToken cancellationToken) =>
        list switch
        {
            TmdbShowBuilderList.Popular => client.GetTvShowPopularAsync(query.Page, query.Language, cancellationToken),
            TmdbShowBuilderList.TopRated => client.GetTvShowTopRatedAsync(query.Page, query.Language, cancellationToken),
            TmdbShowBuilderList.TrendingDay => client.GetTrendingTvAsync(TimeWindow.Day, query.Page, query.Language, cancellationToken),
            TmdbShowBuilderList.TrendingWeek => client.GetTrendingTvAsync(TimeWindow.Week, query.Page, query.Language, cancellationToken),
            TmdbShowBuilderList.AiringToday => client.GetTvShowListAsync(TvShowListType.AiringToday, query.Language, query.Page, query.Timezone, cancellationToken),
            TmdbShowBuilderList.OnTheAir => client.GetTvShowListAsync(TvShowListType.OnTheAir, query.Language, query.Page, query.Timezone, cancellationToken),
            _ => Task.FromResult<SearchContainer<SearchTv>?>(null),
        };

    private static void ApplyMovieDiscoverOptions(DiscoverMovie discover, TmdbBuilderQuery query)
    {
        if (query.Genres.Count > 0)
            discover.IncludeWithAllOfGenre(query.Genres);
        if (query.Year.HasValue)
            discover.WherePrimaryReleaseIsInYear(query.Year.Value);
        if (query.MinVoteAverage.HasValue)
            discover.WhereVoteAverageIsAtLeast(query.MinVoteAverage.Value);
        if (query.MinVoteCount.HasValue)
            discover.WhereVoteCountIsAtLeast(query.MinVoteCount.Value);
        if (!string.IsNullOrWhiteSpace(query.OriginalLanguage))
            discover.WhereOriginalLanguageIs(query.OriginalLanguage);
        if (TryParseMovieSort(query.SortBy, out var sort))
            discover.OrderBy(sort);
    }

    private static void ApplyShowDiscoverOptions(DiscoverTv discover, TmdbBuilderQuery query)
    {
        if (query.Genres.Count > 0)
            discover.WhereGenresInclude(query.Genres);
        if (query.Year.HasValue)
            discover.WhereFirstAirDateIsInYear(query.Year.Value);
        if (query.MinVoteAverage.HasValue)
            discover.WhereVoteAverageIsAtLeast(query.MinVoteAverage.Value);
        if (query.MinVoteCount.HasValue)
            discover.WhereVoteCountIsAtLeast(query.MinVoteCount.Value);
        if (!string.IsNullOrWhiteSpace(query.OriginalLanguage))
            discover.WhereOriginalLanguageIs(query.OriginalLanguage);
        if (TryParseShowSort(query.SortBy, out var sort))
            discover.OrderBy(sort);
    }

    private static bool TryParseMovieSort(string? value, out DiscoverMovieSortBy sort)
    {
        var normalized = NormalizeSort(value);
        if (MovieSortAliases.TryGetValue(normalized, out sort))
            return true;

        return Enum.TryParse(normalized, ignoreCase: true, out sort);
    }

    private static bool TryParseShowSort(string? value, out DiscoverTvShowSortBy sort)
    {
        var normalized = NormalizeSort(value);
        if (ShowSortAliases.TryGetValue(normalized, out sort))
            return true;

        return Enum.TryParse(normalized, ignoreCase: true, out sort);
    }

    private static string NormalizeSort(string? value) =>
        (value ?? string.Empty)
            .Replace(".", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Trim();

    private static TmdbBuilderMovie ToMovie(Movie movie) =>
        new(movie.Id, FirstValue(movie.Title, movie.OriginalTitle, $"TMDB Movie {movie.Id}"), movie.Overview);

    private static TmdbBuilderShow ToShow(TvShow show) =>
        new(show.Id, FirstValue(show.Name, show.OriginalName, $"TMDB Show {show.Id}"), show.Overview);

    private static TmdbBuilderMovie ToMovie(SearchMovie movie) =>
        new(movie.Id, FirstValue(movie.Title, movie.OriginalTitle, $"TMDB Movie {movie.Id}"), movie.Overview);

    private static TmdbBuilderShow ToShow(SearchTv show) =>
        new(show.Id, FirstValue(show.Name, show.OriginalName, $"TMDB Show {show.Id}"), show.Overview);

    private static IReadOnlyList<T> TakeLimit<T>(IEnumerable<T>? values, int limit) =>
        (values ?? [])
            .Take(limit)
            .ToList();

    private static string FirstValue(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static readonly IReadOnlyDictionary<string, DiscoverMovieSortBy> MovieSortAliases = new Dictionary<string, DiscoverMovieSortBy>(StringComparer.OrdinalIgnoreCase)
    {
        ["popularity"] = DiscoverMovieSortBy.PopularityDesc,
        ["popularitydesc"] = DiscoverMovieSortBy.PopularityDesc,
        ["releasedate"] = DiscoverMovieSortBy.ReleaseDateDesc,
        ["releasedatedesc"] = DiscoverMovieSortBy.ReleaseDateDesc,
        ["primaryreleasedate"] = DiscoverMovieSortBy.PrimaryReleaseDateDesc,
        ["primaryreleasedatedesc"] = DiscoverMovieSortBy.PrimaryReleaseDateDesc,
        ["voteaverage"] = DiscoverMovieSortBy.VoteAverageDesc,
        ["voteaveragedesc"] = DiscoverMovieSortBy.VoteAverageDesc,
        ["votecount"] = DiscoverMovieSortBy.VoteCountDesc,
        ["votecountdesc"] = DiscoverMovieSortBy.VoteCountDesc,
    };

    private static readonly IReadOnlyDictionary<string, DiscoverTvShowSortBy> ShowSortAliases = new Dictionary<string, DiscoverTvShowSortBy>(StringComparer.OrdinalIgnoreCase)
    {
        ["popularity"] = DiscoverTvShowSortBy.PopularityDesc,
        ["popularitydesc"] = DiscoverTvShowSortBy.PopularityDesc,
        ["firstairdate"] = DiscoverTvShowSortBy.FirstAirDateDesc,
        ["firstairdatedesc"] = DiscoverTvShowSortBy.FirstAirDateDesc,
        ["primaryreleasedate"] = DiscoverTvShowSortBy.PrimaryReleaseDateDesc,
        ["primaryreleasedatedesc"] = DiscoverTvShowSortBy.PrimaryReleaseDateDesc,
        ["voteaverage"] = DiscoverTvShowSortBy.VoteAverageDesc,
        ["voteaveragedesc"] = DiscoverTvShowSortBy.VoteAverageDesc,
        ["votecount"] = DiscoverTvShowSortBy.VoteCountDesc,
        ["votecountdesc"] = DiscoverTvShowSortBy.VoteCountDesc,
    };
}

public enum TmdbMovieBuilderList
{
    Popular,
    TopRated,
    TrendingDay,
    TrendingWeek,
    NowPlaying,
    Upcoming,
}

public enum TmdbShowBuilderList
{
    Popular,
    TopRated,
    TrendingDay,
    TrendingWeek,
    AiringToday,
    OnTheAir,
}

public sealed record TmdbBuilderQuery
{
    public string Language { get; init; } = "en-US";

    public string? Region { get; init; }

    public string? Timezone { get; init; }

    public int Page { get; init; } = 1;

    public int Limit { get; init; } = 20;

    public int? Year { get; init; }

    public double? MinVoteAverage { get; init; }

    public int? MinVoteCount { get; init; }

    public string? OriginalLanguage { get; init; }

    public string? SortBy { get; init; }

    public IReadOnlyList<int> Genres { get; init; } = [];
}

public sealed record TmdbBuilderMovie(int Id, string Title, string? Overview);

public sealed record TmdbBuilderShow(int Id, string Title, string? Overview);
