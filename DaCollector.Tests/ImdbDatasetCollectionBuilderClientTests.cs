using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DaCollector.Abstractions.Metadata.Enums;
using DaCollector.Server.Collections;
using DaCollector.Server.Settings;
using Moq;
using Xunit;

#nullable enable
namespace DaCollector.Tests;

public class ImdbDatasetCollectionBuilderClientTests
{
    [Fact]
    public async Task GetByIds_ReturnsTitles_WhenIdsExist()
    {
        using var dir = new TempDatasetDirectory();
        dir.WriteBasics("tt0076759\tmovie\tStar Wars\tStar Wars\t0\t1977\t\\N\t121\tAction,Adventure");

        var client = CreateClient(dir.Path);

        var titles = await client.GetByIds(["tt0076759"], MediaKind.Movie);

        var title = Assert.Single(titles);
        Assert.Equal("tt0076759", title.Id);
        Assert.Equal("Star Wars", title.Title);
        Assert.Equal(MediaKind.Movie, title.Kind);
    }

    [Fact]
    public async Task GetByIds_ReturnsEmpty_WhenKindFilterDoesNotMatch()
    {
        using var dir = new TempDatasetDirectory();
        dir.WriteBasics("tt0076759\tmovie\tStar Wars\tStar Wars\t0\t1977\t\\N\t121\tAction");

        var client = CreateClient(dir.Path);

        var titles = await client.GetByIds(["tt0076759"], MediaKind.Show);

        Assert.Empty(titles);
    }

    [Fact]
    public async Task GetByIds_SkipsMalformedRows_LessThan9Fields()
    {
        using var dir = new TempDatasetDirectory();
        dir.WriteBasics(
            "tt0076759\tmovie\tStar Wars\tStar Wars\t0\t1977\t\\N\t121\tAction",
            "tt9999999\tmovie\tBad Row" // only 3 tab-delimited fields
        );

        var client = CreateClient(dir.Path);

        var titles = await client.GetByIds(["tt0076759", "tt9999999"], MediaKind.Movie);

        var title = Assert.Single(titles);
        Assert.Equal("tt0076759", title.Id);
    }

    [Fact]
    public async Task GetByIds_SkipsUnknownTitleKinds()
    {
        using var dir = new TempDatasetDirectory();
        dir.WriteBasics("tt0066921\ttvepisode\tSome Episode\tSome Episode\t0\t1968\t\\N\t25\tDrama");

        var client = CreateClient(dir.Path);

        var titles = await client.GetByIds(["tt0066921"], MediaKind.Unknown);

        Assert.Empty(titles);
    }

    [Fact]
    public async Task Search_FiltersByKind_ReturnsOnlyMovies()
    {
        using var dir = new TempDatasetDirectory();
        dir.WriteBasics(
            "tt0076759\tmovie\tStar Wars\tStar Wars\t0\t1977\t\\N\t121\tAction",
            "tt0944947\ttvseries\tGame of Thrones\tGame of Thrones\t0\t2011\t2019\t\\N\tDrama"
        );

        var client = CreateClient(dir.Path);

        var results = await client.Search(new ImdbBuilderQuery { Kind = MediaKind.Movie, Limit = 20 });

        var title = Assert.Single(results);
        Assert.Equal(MediaKind.Movie, title.Kind);
        Assert.Equal("Star Wars", title.Title);
    }

    [Fact]
    public async Task GetChart_Throws_WhenRatingsFileAbsent()
    {
        using var dir = new TempDatasetDirectory();
        dir.WriteBasics("tt0076759\tmovie\tStar Wars\tStar Wars\t0\t1977\t\\N\t121\tAction");

        var client = CreateClient(dir.Path);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await client.GetChart(new ImdbBuilderQuery { Kind = MediaKind.Movie }));
        Assert.Contains("required", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetChart_ReturnsTitles_WhenRatingsFilePresent()
    {
        using var dir = new TempDatasetDirectory();
        dir.WriteBasics("tt0076759\tmovie\tStar Wars\tStar Wars\t0\t1977\t\\N\t121\tAction");
        dir.WriteRatings("tt0076759\t8.6\t1300000");

        var client = CreateClient(dir.Path);

        var results = await client.GetChart(new ImdbBuilderQuery { Kind = MediaKind.Movie, Limit = 5 });

        var title = Assert.Single(results);
        Assert.Equal("tt0076759", title.Id);
    }

    [Fact]
    public async Task GetSnapshot_CachesData_AfterInitialLoad()
    {
        using var dir = new TempDatasetDirectory();
        dir.WriteBasics("tt0076759\tmovie\tStar Wars\tStar Wars\t0\t1977\t\\N\t121\tAction");

        var client = CreateClient(dir.Path);

        // Prime the snapshot
        await client.GetByIds(["tt0076759"], MediaKind.Movie);

        // Delete the file — a second call with the same path should use the cached snapshot
        dir.DeleteBasics();

        var titles = await client.GetByIds(["tt0076759"], MediaKind.Movie);

        var title = Assert.Single(titles);
        Assert.Equal("tt0076759", title.Id);
    }

    [Fact]
    public async Task GetByIds_Throws_WhenPathNotFound()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing");
        var client = CreateClient(missingPath);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await client.GetByIds(["tt0076759"], MediaKind.Movie));
        Assert.Contains("not found", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetByIds_Throws_WhenBasicsFileMissingFromDirectory()
    {
        using var dir = new TempDatasetDirectory();
        // Empty directory — no title.basics.tsv written

        var client = CreateClient(dir.Path);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await client.GetByIds(["tt0076759"], MediaKind.Movie));
        Assert.Contains("title.basics.tsv", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetByIds_Throws_WhenDatasetPathNotConfigured()
    {
        var client = CreateClient(string.Empty);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await client.GetByIds(["tt0076759"], MediaKind.Movie));
        Assert.Contains("not configured", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Search_MatchesOnPrimaryOrOriginalTitle()
    {
        using var dir = new TempDatasetDirectory();
        dir.WriteBasics(
            "tt0076759\tmovie\tStar Wars\tStar Wars\t0\t1977\t\\N\t121\tAction",
            "tt0110912\tmovie\tPulp Fiction\tPulp Fiction\t0\t1994\t\\N\t154\tCrime,Drama"
        );

        var client = CreateClient(dir.Path);

        var results = await client.Search(new ImdbBuilderQuery { SearchText = "Pulp Fiction", Kind = MediaKind.Unknown, Limit = 5 });

        var title = Assert.Single(results);
        Assert.Equal("Pulp Fiction", title.Title);
    }

    private static ImdbDatasetCollectionBuilderClient CreateClient(string datasetPath)
    {
        var settings = new ServerSettings();
        settings.IMDb.DatasetPath = datasetPath;

        var settingsProvider = new Mock<ISettingsProvider>();
        settingsProvider.Setup(p => p.GetSettings(It.IsAny<bool>())).Returns(settings);

        return new(settingsProvider.Object);
    }

    private sealed class TempDatasetDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        private string? _basicsPath;

        public TempDatasetDirectory() => Directory.CreateDirectory(Path);

        public void WriteBasics(params string[] dataRows)
        {
            _basicsPath = System.IO.Path.Combine(Path, "title.basics.tsv");
            var lines = new List<string>
            {
                "tconst\ttitleType\tprimaryTitle\toriginalTitle\tisAdult\tstartYear\tendYear\truntimeMinutes\tgenres",
            };
            lines.AddRange(dataRows);
            File.WriteAllLines(_basicsPath, lines);
        }

        public void WriteRatings(params string[] dataRows)
        {
            var ratingsPath = System.IO.Path.Combine(Path, "title.ratings.tsv");
            var lines = new List<string> { "tconst\taverageRating\tnumVotes" };
            lines.AddRange(dataRows);
            File.WriteAllLines(ratingsPath, lines);
        }

        public void DeleteBasics()
        {
            if (_basicsPath is not null && File.Exists(_basicsPath))
                File.Delete(_basicsPath);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
