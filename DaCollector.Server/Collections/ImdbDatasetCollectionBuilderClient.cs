using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DaCollector.Abstractions.Metadata.Enums;
using DaCollector.Server.Settings;

#nullable enable
namespace DaCollector.Server.Collections;

public interface IImdbCollectionBuilderClient
{
    Task<IReadOnlyList<ImdbBuilderTitle>> GetByIds(IReadOnlyList<string> imdbIds, MediaKind kind, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ImdbBuilderTitle>> Search(ImdbBuilderQuery query, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ImdbBuilderTitle>> GetChart(ImdbBuilderQuery query, CancellationToken cancellationToken = default);
}

public class ImdbDatasetCollectionBuilderClient(ISettingsProvider settingsProvider) : IImdbCollectionBuilderClient
{
    private readonly object _snapshotLock = new();
    private DatasetSnapshot? _snapshot;

    public Task<IReadOnlyList<ImdbBuilderTitle>> GetByIds(IReadOnlyList<string> imdbIds, MediaKind kind, CancellationToken cancellationToken = default)
    {
        var snapshot = GetSnapshot(cancellationToken);
        var titles = imdbIds
            .Select(id => snapshot.Titles.GetValueOrDefault(id))
            .Where(title => title is not null && KindMatches(title.Kind, kind))
            .Select(title => ToBuilderTitle(title!, snapshot))
            .ToList();

        return Task.FromResult<IReadOnlyList<ImdbBuilderTitle>>(titles);
    }

    public Task<IReadOnlyList<ImdbBuilderTitle>> Search(ImdbBuilderQuery query, CancellationToken cancellationToken = default)
    {
        var snapshot = GetSnapshot(cancellationToken);
        var searchText = query.SearchText?.Trim();
        var titles = ApplyFilters(snapshot.Titles.Values, snapshot, query)
            .Where(title => string.IsNullOrWhiteSpace(searchText) || Contains(title.PrimaryTitle, searchText) || Contains(title.OriginalTitle, searchText))
            .OrderBy(title => GetSearchRank(title, searchText))
            .ThenByDescending(title => snapshot.Ratings.GetValueOrDefault(title.Id)?.AverageRating ?? 0)
            .ThenByDescending(title => snapshot.Ratings.GetValueOrDefault(title.Id)?.VoteCount ?? 0)
            .ThenBy(title => title.PrimaryTitle, StringComparer.OrdinalIgnoreCase)
            .Take(query.Limit)
            .Select(title => ToBuilderTitle(title, snapshot))
            .ToList();

        return Task.FromResult<IReadOnlyList<ImdbBuilderTitle>>(titles);
    }

    public Task<IReadOnlyList<ImdbBuilderTitle>> GetChart(ImdbBuilderQuery query, CancellationToken cancellationToken = default)
    {
        var snapshot = GetSnapshot(cancellationToken);
        if (snapshot.Ratings.Count == 0)
            throw new InvalidOperationException("IMDb title.ratings.tsv dataset file is required for imdb_chart previews.");

        var titles = ApplyFilters(snapshot.Titles.Values, snapshot, query)
            .Where(title => snapshot.Ratings.ContainsKey(title.Id))
            .OrderByDescending(title => snapshot.Ratings[title.Id].AverageRating)
            .ThenByDescending(title => snapshot.Ratings[title.Id].VoteCount)
            .ThenBy(title => title.PrimaryTitle, StringComparer.OrdinalIgnoreCase)
            .Take(query.Limit)
            .Select(title => ToBuilderTitle(title, snapshot))
            .ToList();

        return Task.FromResult<IReadOnlyList<ImdbBuilderTitle>>(titles);
    }

    private DatasetSnapshot GetSnapshot(CancellationToken cancellationToken)
    {
        var datasetPath = settingsProvider.GetSettings().IMDb.DatasetPath?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(datasetPath))
            throw new InvalidOperationException("IMDb dataset path is not configured.");

        lock (_snapshotLock)
        {
            if (_snapshot is { DatasetPath: var loadedPath } && string.Equals(loadedPath, datasetPath, StringComparison.OrdinalIgnoreCase))
                return _snapshot;

            var files = ResolveDatasetFiles(datasetPath);
            _snapshot = new(datasetPath, ReadTitles(files.BasicsPath, cancellationToken), ReadRatings(files.RatingsPath, cancellationToken));
            return _snapshot;
        }
    }

    private static IEnumerable<ImdbDatasetTitle> ApplyFilters(IEnumerable<ImdbDatasetTitle> titles, DatasetSnapshot snapshot, ImdbBuilderQuery query) =>
        titles.Where(title =>
        {
            if (!KindMatches(title.Kind, query.Kind))
                return false;
            if (query.StartYear.HasValue && (!title.StartYear.HasValue || title.StartYear.Value < query.StartYear.Value))
                return false;
            if (query.EndYear.HasValue && (!title.StartYear.HasValue || title.StartYear.Value > query.EndYear.Value))
                return false;
            if (query.MinRating.HasValue && (!snapshot.Ratings.TryGetValue(title.Id, out var ratingForAverage) || ratingForAverage.AverageRating < query.MinRating.Value))
                return false;
            if (query.MinVotes.HasValue && (!snapshot.Ratings.TryGetValue(title.Id, out var ratingForVotes) || ratingForVotes.VoteCount < query.MinVotes.Value))
                return false;

            return true;
        });

    private static DatasetFiles ResolveDatasetFiles(string datasetPath)
    {
        if (File.Exists(datasetPath))
            return new(datasetPath, null);
        if (!Directory.Exists(datasetPath))
            throw new InvalidOperationException($"IMDb dataset path '{datasetPath}' was not found.");

        var basics = FindDatasetFile(datasetPath, "title.basics.tsv")
            ?? throw new InvalidOperationException("IMDb title.basics.tsv dataset file was not found.");
        var ratings = FindDatasetFile(datasetPath, "title.ratings.tsv");
        return new(basics, ratings);
    }

    private static string? FindDatasetFile(string directory, string fileName)
    {
        var plainPath = Path.Combine(directory, fileName);
        if (File.Exists(plainPath))
            return plainPath;

        var gzipPath = plainPath + ".gz";
        return File.Exists(gzipPath) ? gzipPath : null;
    }

    private static Dictionary<string, ImdbDatasetTitle> ReadTitles(string path, CancellationToken cancellationToken)
    {
        var titles = new Dictionary<string, ImdbDatasetTitle>(StringComparer.OrdinalIgnoreCase);
        using var reader = OpenReader(path);
        _ = reader.ReadLine();
        while (reader.ReadLine() is { } line)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fields = line.Split('\t');
            if (fields.Length < 9)
                continue;

            var kind = ParseTitleKind(fields[1]);
            if (kind is MediaKind.Unknown)
                continue;

            var id = CleanValue(fields[0]);
            if (string.IsNullOrWhiteSpace(id))
                continue;

            titles[id] = new(
                id,
                kind,
                FirstValue(CleanValue(fields[2]), CleanValue(fields[3]), id),
                CleanValue(fields[3]),
                ParseNullableInt(fields[5]),
                CleanValue(fields[8])
            );
        }

        return titles;
    }

    private static Dictionary<string, ImdbDatasetRating> ReadRatings(string? path, CancellationToken cancellationToken)
    {
        var ratings = new Dictionary<string, ImdbDatasetRating>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(path))
            return ratings;

        using var reader = OpenReader(path);
        _ = reader.ReadLine();
        while (reader.ReadLine() is { } line)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fields = line.Split('\t');
            if (fields.Length < 3)
                continue;
            if (!double.TryParse(fields[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var averageRating))
                continue;
            if (!int.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var voteCount))
                continue;

            ratings[fields[0]] = new(averageRating, voteCount);
        }

        return ratings;
    }

    private static StreamReader OpenReader(string path)
    {
        var stream = File.OpenRead(path);
        if (path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
            return new(new GZipStream(stream, CompressionMode.Decompress));

        return new(stream);
    }

    private static ImdbBuilderTitle ToBuilderTitle(ImdbDatasetTitle title, DatasetSnapshot snapshot)
    {
        snapshot.Ratings.TryGetValue(title.Id, out var rating);
        return new(title.Id, title.Kind, title.PrimaryTitle, BuildSummary(title, rating));
    }

    private static string? BuildSummary(ImdbDatasetTitle title, ImdbDatasetRating? rating)
    {
        var parts = new List<string>();
        if (title.StartYear.HasValue)
            parts.Add(title.StartYear.Value.ToString(CultureInfo.InvariantCulture));
        if (!string.IsNullOrWhiteSpace(title.Genres))
            parts.Add(title.Genres);
        if (rating is not null)
            parts.Add($"IMDb {rating.AverageRating.ToString("0.0", CultureInfo.InvariantCulture)} ({rating.VoteCount.ToString("N0", CultureInfo.InvariantCulture)} votes)");

        return parts.Count == 0 ? null : string.Join(" | ", parts);
    }

    private static int GetSearchRank(ImdbDatasetTitle title, string? searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return 0;
        if (string.Equals(title.PrimaryTitle, searchText, StringComparison.OrdinalIgnoreCase))
            return 0;
        if (title.PrimaryTitle.StartsWith(searchText, StringComparison.OrdinalIgnoreCase))
            return 1;
        if (!string.IsNullOrWhiteSpace(title.OriginalTitle) && string.Equals(title.OriginalTitle, searchText, StringComparison.OrdinalIgnoreCase))
            return 2;
        if (!string.IsNullOrWhiteSpace(title.OriginalTitle) && title.OriginalTitle.StartsWith(searchText, StringComparison.OrdinalIgnoreCase))
            return 3;

        return 4;
    }

    private static bool Contains(string? value, string searchText) =>
        value?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false;

    private static bool KindMatches(MediaKind titleKind, MediaKind requestedKind) =>
        requestedKind is MediaKind.Unknown || titleKind == requestedKind;

    private static MediaKind ParseTitleKind(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "movie" or "tvmovie" => MediaKind.Movie,
            "tvseries" or "tvminiseries" => MediaKind.Show,
            _ => MediaKind.Unknown,
        };

    private static int? ParseNullableInt(string value) =>
        int.TryParse(CleanValue(value), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;

    private static string? CleanValue(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) || string.Equals(trimmed, @"\N", StringComparison.Ordinal) ? null : trimmed;
    }

    private static string FirstValue(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private sealed record DatasetFiles(string BasicsPath, string? RatingsPath);

    private sealed record DatasetSnapshot(
        string DatasetPath,
        Dictionary<string, ImdbDatasetTitle> Titles,
        Dictionary<string, ImdbDatasetRating> Ratings
    );

    private sealed record ImdbDatasetTitle(
        string Id,
        MediaKind Kind,
        string PrimaryTitle,
        string? OriginalTitle,
        int? StartYear,
        string? Genres
    );

    private sealed record ImdbDatasetRating(double AverageRating, int VoteCount);
}

public sealed record ImdbBuilderQuery
{
    public MediaKind Kind { get; init; } = MediaKind.Unknown;

    public string? SearchText { get; init; }

    public int Limit { get; init; } = 20;

    public int? StartYear { get; init; }

    public int? EndYear { get; init; }

    public double? MinRating { get; init; }

    public int? MinVotes { get; init; }
}

public sealed record ImdbBuilderTitle(string Id, MediaKind Kind, string Title, string? Summary);
