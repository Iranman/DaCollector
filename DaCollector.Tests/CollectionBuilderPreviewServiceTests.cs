using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DaCollector.Abstractions.Collections;
using DaCollector.Abstractions.Metadata.Enums;
using DaCollector.Server.Collections;
using DaCollector.Server.Settings;
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

    [Fact]
    public async Task Preview_ImdbId_ResolvesTitlesFromConfiguredDataset()
    {
        using var dataset = new ImdbDatasetTempDirectory();
        var service = CreateService(imdb: CreateImdbClient(dataset.Path));

        var preview = await service.Preview(new()
        {
            Builder = "imdb_id",
            Options = new Dictionary<string, string> { ["ids"] = "tt0111161,1375666" },
        });

        Assert.Collection(
            preview.Items,
            item =>
            {
                Assert.Equal(MediaKind.Movie, item.ExternalID.Kind);
                Assert.Equal("tt0111161", item.ExternalID.Value);
                Assert.Equal("The Shawshank Redemption", item.Title);
                Assert.Contains("IMDb 9.3", item.Summary ?? string.Empty);
            },
            item =>
            {
                Assert.Equal("tt1375666", item.ExternalID.Value);
                Assert.Equal("Inception", item.Title);
            }
        );
        Assert.Empty(preview.Warnings);
    }

    [Fact]
    public async Task Preview_ImdbSearch_FiltersConfiguredDataset()
    {
        using var dataset = new ImdbDatasetTempDirectory();
        var service = CreateService(imdb: CreateImdbClient(dataset.Path));

        var preview = await service.Preview(new()
        {
            Builder = "imdb_search",
            Options = new Dictionary<string, string>
            {
                ["query"] = "Game",
                ["kind"] = "show",
            },
        });

        var item = Assert.Single(preview.Items);
        Assert.Equal(MediaKind.Show, item.ExternalID.Kind);
        Assert.Equal("tt0944947", item.ExternalID.Value);
        Assert.Equal("Game of Thrones", item.Title);
        Assert.Empty(preview.Warnings);
    }

    [Fact]
    public async Task Preview_ImdbChart_UsesRatingsOrderAndFilters()
    {
        using var dataset = new ImdbDatasetTempDirectory();
        var service = CreateService(imdb: CreateImdbClient(dataset.Path));

        var preview = await service.Preview(new()
        {
            Builder = "imdb_chart",
            Options = new Dictionary<string, string>
            {
                ["kind"] = "show",
                ["limit"] = "2",
            },
        });

        Assert.Collection(
            preview.Items,
            item =>
            {
                Assert.Equal("tt0903747", item.ExternalID.Value);
                Assert.Equal("Breaking Bad", item.Title);
            },
            item =>
            {
                Assert.Equal("tt0944947", item.ExternalID.Value);
                Assert.Equal("Game of Thrones", item.Title);
            }
        );
        Assert.Empty(preview.Warnings);
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

    private static IImdbCollectionBuilderClient CreateImdbClient(string datasetPath)
    {
        var settings = new ServerSettings
        {
            IMDb =
            {
                Enabled = true,
                DatasetPath = datasetPath,
            },
        };

        return new ImdbDatasetCollectionBuilderClient(new FakeSettingsProvider(settings));
    }

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

    private sealed class FakeSettingsProvider(IServerSettings settings) : ISettingsProvider
    {
        public IServerSettings GetSettings(bool copy = false) => settings;

        public void SaveSettings(IServerSettings newSettings)
        {
        }

        public void SaveSettings()
        {
        }

        public void DebugSettingsToLog()
        {
        }
    }

    private sealed class ImdbDatasetTempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"daccollector-imdb-{Guid.NewGuid():N}");

        public ImdbDatasetTempDirectory()
        {
            Directory.CreateDirectory(Path);
            File.WriteAllText(
                System.IO.Path.Combine(Path, "title.basics.tsv"),
                string.Join(Environment.NewLine,
                    "tconst\ttitleType\tprimaryTitle\toriginalTitle\tisAdult\tstartYear\tendYear\truntimeMinutes\tgenres",
                    "tt0111161\tmovie\tThe Shawshank Redemption\tThe Shawshank Redemption\t0\t1994\t\\N\t142\tDrama",
                    "tt1375666\tmovie\tInception\tInception\t0\t2010\t\\N\t148\tAction,Sci-Fi",
                    "tt0944947\ttvSeries\tGame of Thrones\tGame of Thrones\t0\t2011\t2019\t57\tAdventure,Drama",
                    "tt0903747\ttvSeries\tBreaking Bad\tBreaking Bad\t0\t2008\t2013\t49\tCrime,Drama",
                    "tt0000001\tshort\tSkipped Short\tSkipped Short\t0\t1894\t\\N\t1\tDocumentary"
                )
            );
            File.WriteAllText(
                System.IO.Path.Combine(Path, "title.ratings.tsv"),
                string.Join(Environment.NewLine,
                    "tconst\taverageRating\tnumVotes",
                    "tt0111161\t9.3\t2900000",
                    "tt1375666\t8.8\t2500000",
                    "tt0944947\t9.2\t2400000",
                    "tt0903747\t9.5\t2200000"
                )
            );
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
