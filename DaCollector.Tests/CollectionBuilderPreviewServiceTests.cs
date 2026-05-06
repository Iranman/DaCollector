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
        var service = new CollectionBuilderPreviewService(tmdb);

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
        var service = new CollectionBuilderPreviewService(tmdb);

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
        var service = new CollectionBuilderPreviewService(new FakeTmdbCollectionBuilderClient());

        var preview = await service.Preview(new()
        {
            Builder = "tmdb_now_playing",
            Kind = MediaKind.Show,
        });

        Assert.Empty(preview.Items);
        Assert.Contains(preview.Warnings, warning => warning.Contains("does not support media kind"));
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
}
