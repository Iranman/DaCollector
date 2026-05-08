using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DaCollector.Abstractions.Collections;
using DaCollector.Abstractions.Metadata.Enums;
using DaCollector.Server.Collections;
using Xunit;

#nullable enable
namespace DaCollector.Tests;

public class CollectionBuilderPreviewServiceTests
{
    [Fact]
    public async Task Preview_TmdbMovie_FetchesRemoteWhenMovieIsNotCached()
    {
        var tmdb = new FakeTmdbCollectionBuilderClient
        {
            Movies = { [11] = new(11, "Star Wars", "A space opera.") },
        };
        var service = CreateService(tmdb);

        var preview = await service.Preview(new()
        {
            Builder = "tmdb_movie",
            Options = new Dictionary<string, string> { ["id"] = "11" },
        });

        var item = Assert.Single(preview.Items);
        Assert.Equal(ExternalProvider.TMDB, item.ExternalID.Provider);
        Assert.Equal(MediaKind.Movie, item.ExternalID.Kind);
        Assert.Equal("11", item.ExternalID.Value);
        Assert.Equal("Star Wars", item.Title);
        Assert.Empty(preview.Warnings);
    }

    [Fact]
    public async Task Preview_TmdbShow_FetchesRemoteWhenShowIsNotCached()
    {
        var tmdb = new FakeTmdbCollectionBuilderClient
        {
            Shows = { [22] = new(22, "The Expanse", "A space drama.") },
        };
        var service = CreateService(tmdb);

        var preview = await service.Preview(new()
        {
            Builder = "tmdb_show",
            Options = new Dictionary<string, string> { ["id"] = "22" },
        });

        var item = Assert.Single(preview.Items);
        Assert.Equal(ExternalProvider.TMDB, item.ExternalID.Provider);
        Assert.Equal(MediaKind.Show, item.ExternalID.Kind);
        Assert.Equal("22", item.ExternalID.Value);
        Assert.Equal("The Expanse", item.Title);
        Assert.Empty(preview.Warnings);
    }

    [Fact]
    public async Task Preview_TmdbCollection_ReturnsMovieParts()
    {
        var tmdb = new FakeTmdbCollectionBuilderClient
        {
            CollectionMovies =
            [
                new(100, "The Matrix", "A simulation."),
                new(101, "The Matrix Reloaded", "A sequel."),
            ],
        };
        var service = CreateService(tmdb);

        var preview = await service.Preview(new()
        {
            Builder = "tmdb_collection",
            Options = new Dictionary<string, string> { ["id"] = "10" },
        });

        Assert.Collection(
            preview.Items,
            item =>
            {
                Assert.Equal(ExternalProvider.TMDB, item.ExternalID.Provider);
                Assert.Equal(MediaKind.Movie, item.ExternalID.Kind);
                Assert.Equal("100", item.ExternalID.Value);
                Assert.Equal("The Matrix", item.Title);
            },
            item =>
            {
                Assert.Equal("101", item.ExternalID.Value);
                Assert.Equal("The Matrix Reloaded", item.Title);
            }
        );
        Assert.Empty(preview.Warnings);
    }

    [Fact]
    public async Task Preview_DuplicateProviderIds_AreCollapsed()
    {
        var tmdb = new FakeTmdbCollectionBuilderClient
        {
            Movies = { [11] = new(11, "Star Wars", null) },
        };
        var service = CreateService(tmdb);

        var preview = await service.Preview(new()
        {
            Builder = "tmdb_movie",
            Options = new Dictionary<string, string> { ["ids"] = "11,11" },
        });

        var item = Assert.Single(preview.Items);
        Assert.Equal("11", item.ExternalID.Value);
        Assert.Empty(preview.Warnings);
    }

    [Fact]
    public async Task Preview_DuplicateTitlesWithDifferentExternalIds_AreKept()
    {
        var tmdb = new FakeTmdbCollectionBuilderClient
        {
            PopularMovies = [new(1, "The Office", null)],
            PopularShows = [new(2, "The Office", null)],
        };
        var service = CreateService(tmdb);

        var preview = await service.Preview(new()
        {
            Builder = "tmdb_popular",
            Options = new Dictionary<string, string> { ["limit"] = "10" },
        });

        Assert.Equal(2, preview.Items.Count);
        Assert.Contains(preview.Items, item => item.ExternalID.Kind == MediaKind.Movie && item.Title == "The Office");
        Assert.Contains(preview.Items, item => item.ExternalID.Kind == MediaKind.Show && item.Title == "The Office");
        Assert.Empty(preview.Warnings);
    }

    [Fact]
    public async Task Preview_TmdbPopular_ReturnsMoviesAndShowsWhenKindIsUnknown()
    {
        var tmdb = new FakeTmdbCollectionBuilderClient
        {
            PopularMovies = [new(1, "Movie One", null)],
            PopularShows = [new(2, "Show Two", null)],
        };
        var service = CreateService(tmdb);

        var preview = await service.Preview(new()
        {
            Builder = "tmdb_popular",
            Options = new Dictionary<string, string> { ["limit"] = "10" },
        });

        Assert.Contains(preview.Items, item => item.ExternalID.Kind == MediaKind.Movie && item.ExternalID.Value == "1");
        Assert.Contains(preview.Items, item => item.ExternalID.Kind == MediaKind.Show && item.ExternalID.Value == "2");
        Assert.Empty(preview.Warnings);
    }

    [Fact]
    public async Task Preview_TmdbNowPlaying_RejectsShowKind()
    {
        var service = CreateService();

        var preview = await service.Preview(new()
        {
            Builder = "tmdb_now_playing",
            Kind = MediaKind.Show,
        });

        Assert.Empty(preview.Items);
        Assert.Contains(preview.Warnings, warning => warning.Contains("does not support media kind"));
    }

    [Theory]
    [InlineData("imdb_id")]
    [InlineData("imdb_list")]
    [InlineData("imdb_chart")]
    [InlineData("imdb_search")]
    public async Task Preview_ImdbBuilders_AreNotExposed(string builder)
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() => service.Preview(new()
        {
            Builder = builder,
        }));

        Assert.DoesNotContain(CollectionBuilderCatalog.All.Values, descriptor => descriptor.Provider == ExternalProvider.IMDb);
    }

    [Fact]
    public async Task Preview_TvdbMovie_FetchesRemoteTitle()
    {
        var tvdb = new FakeTvdbCollectionBuilderClient
        {
            Movies = { [101] = new(101, MediaKind.Movie, "The TVDB Movie", "A TVDB movie.") },
        };
        var service = CreateService(tvdb: tvdb);

        var preview = await service.Preview(new()
        {
            Builder = "tvdb_movie",
            Options = new Dictionary<string, string> { ["id"] = "101" },
        });

        var item = Assert.Single(preview.Items);
        Assert.Equal(ExternalProvider.TVDB, item.ExternalID.Provider);
        Assert.Equal(MediaKind.Movie, item.ExternalID.Kind);
        Assert.Equal("101", item.ExternalID.Value);
        Assert.Equal("The TVDB Movie", item.Title);
        Assert.Equal("A TVDB movie.", item.Summary);
        Assert.Empty(preview.Warnings);
    }

    [Fact]
    public async Task Preview_TvdbShow_FetchesRemoteTitle()
    {
        var tvdb = new FakeTvdbCollectionBuilderClient
        {
            Shows = { [202] = new(202, MediaKind.Show, "The TVDB Show", "A TVDB show.") },
        };
        var service = CreateService(tvdb: tvdb);

        var preview = await service.Preview(new()
        {
            Builder = "tvdb_show",
            Options = new Dictionary<string, string> { ["id"] = "202" },
        });

        var item = Assert.Single(preview.Items);
        Assert.Equal(ExternalProvider.TVDB, item.ExternalID.Provider);
        Assert.Equal(MediaKind.Show, item.ExternalID.Kind);
        Assert.Equal("202", item.ExternalID.Value);
        Assert.Equal("The TVDB Show", item.Title);
        Assert.Equal("A TVDB show.", item.Summary);
        Assert.Empty(preview.Warnings);
    }

    [Fact]
    public async Task Preview_TvdbList_ReturnsMixedTitlesWhenKindIsUnknown()
    {
        var tvdb = new FakeTvdbCollectionBuilderClient
        {
            Lists =
            {
                [7] =
                [
                    new(101, MediaKind.Movie, "Movie From List", null),
                    new(202, MediaKind.Show, "Show From List", null),
                ],
            },
        };
        var service = CreateService(tvdb: tvdb);

        var preview = await service.Preview(new()
        {
            Builder = "tvdb_list",
            Options = new Dictionary<string, string> { ["id"] = "7" },
        });

        Assert.Contains(preview.Items, item => item.ExternalID.Kind == MediaKind.Movie && item.ExternalID.Value == "101");
        Assert.Contains(preview.Items, item => item.ExternalID.Kind == MediaKind.Show && item.ExternalID.Value == "202");
        Assert.Empty(preview.Warnings);
    }

    [Fact]
    public async Task Preview_TvdbList_FiltersRequestedKind()
    {
        var tvdb = new FakeTvdbCollectionBuilderClient
        {
            Lists =
            {
                [7] =
                [
                    new(101, MediaKind.Movie, "Movie From List", null),
                    new(202, MediaKind.Show, "Show From List", null),
                ],
            },
        };
        var service = CreateService(tvdb: tvdb);

        var preview = await service.Preview(new()
        {
            Builder = "tvdb_list",
            Options = new Dictionary<string, string>
            {
                ["id"] = "7",
                ["kind"] = "show",
            },
        });

        var item = Assert.Single(preview.Items);
        Assert.Equal(MediaKind.Show, item.ExternalID.Kind);
        Assert.Equal("202", item.ExternalID.Value);
        Assert.Empty(preview.Warnings);
    }

    private static CollectionBuilderPreviewService CreateService(
        ITmdbCollectionBuilderClient? tmdb = null,
        IImdbCollectionBuilderClient? imdb = null,
        ITvdbCollectionBuilderClient? tvdb = null
    ) =>
        new(
            tmdb ?? new FakeTmdbCollectionBuilderClient(),
            imdb ?? new FakeImdbCollectionBuilderClient(),
            tvdb ?? new FakeTvdbCollectionBuilderClient()
        );

    private sealed class FakeTmdbCollectionBuilderClient : ITmdbCollectionBuilderClient
    {
        public Dictionary<int, TmdbBuilderMovie> Movies { get; } = [];

        public Dictionary<int, TmdbBuilderShow> Shows { get; } = [];

        public IReadOnlyList<TmdbBuilderMovie> CollectionMovies { get; init; } = [];

        public IReadOnlyList<TmdbBuilderMovie> PopularMovies { get; init; } = [];

        public IReadOnlyList<TmdbBuilderShow> PopularShows { get; init; } = [];

        public Task<TmdbBuilderMovie?> GetMovie(int id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Movies.GetValueOrDefault(id));

        public Task<TmdbBuilderShow?> GetShow(int id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Shows.GetValueOrDefault(id));

        public Task<IReadOnlyList<TmdbBuilderMovie>> GetCollectionMovies(int id, TmdbBuilderQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult(CollectionMovies);

        public Task<IReadOnlyList<TmdbBuilderMovie>> GetMovies(TmdbMovieBuilderList list, TmdbBuilderQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TmdbBuilderMovie>>(list is TmdbMovieBuilderList.Popular ? PopularMovies.Take(query.Limit).ToList() : []);

        public Task<IReadOnlyList<TmdbBuilderShow>> GetShows(TmdbShowBuilderList list, TmdbBuilderQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TmdbBuilderShow>>(list is TmdbShowBuilderList.Popular ? PopularShows.Take(query.Limit).ToList() : []);

        public Task<IReadOnlyList<TmdbBuilderMovie>> DiscoverMovies(TmdbBuilderQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TmdbBuilderMovie>>([]);

        public Task<IReadOnlyList<TmdbBuilderShow>> DiscoverShows(TmdbBuilderQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TmdbBuilderShow>>([]);
    }

    private sealed class FakeImdbCollectionBuilderClient : IImdbCollectionBuilderClient
    {
        public Task<IReadOnlyList<ImdbBuilderTitle>> GetByIds(IReadOnlyList<string> imdbIds, MediaKind kind, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ImdbBuilderTitle>>([]);

        public Task<IReadOnlyList<ImdbBuilderTitle>> Search(ImdbBuilderQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ImdbBuilderTitle>>([]);

        public Task<IReadOnlyList<ImdbBuilderTitle>> GetChart(ImdbBuilderQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ImdbBuilderTitle>>([]);
    }

    private sealed class FakeTvdbCollectionBuilderClient : ITvdbCollectionBuilderClient
    {
        public Dictionary<int, TvdbBuilderTitle> Movies { get; } = [];

        public Dictionary<int, TvdbBuilderTitle> Shows { get; } = [];

        public Dictionary<int, IReadOnlyList<TvdbBuilderTitle>> Lists { get; } = [];

        public Task<TvdbBuilderTitle?> GetMovie(int id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Movies.GetValueOrDefault(id));

        public Task<TvdbBuilderTitle?> GetShow(int id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Shows.GetValueOrDefault(id));

        public Task<IReadOnlyList<TvdbBuilderTitle>> GetList(int id, TvdbBuilderQuery query, CancellationToken cancellationToken = default)
        {
            if (!Lists.TryGetValue(id, out var items))
                return Task.FromResult<IReadOnlyList<TvdbBuilderTitle>>([]);

            var filtered = items
                .Where(item => query.Kind is MediaKind.Unknown || item.Kind == query.Kind)
                .Take(query.Limit)
                .ToList();
            return Task.FromResult<IReadOnlyList<TvdbBuilderTitle>>(filtered);
        }
    }

}
