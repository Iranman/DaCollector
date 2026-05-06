using System;
using System.Collections.Generic;
using System.Linq;
using DaCollector.Abstractions.Collections;
using DaCollector.Abstractions.Metadata;
using DaCollector.Abstractions.Metadata.Enums;
using DaCollector.Server.Repositories;

#nullable enable
namespace DaCollector.Server.Collections;

/// <summary>
/// Previews collection builder output from local provider data.
/// </summary>
public class CollectionBuilderPreviewService
{
    private static readonly string[] ValueOptionKeys =
    [
        "id",
        "ids",
        "value",
        "values",
    ];

    public CollectionBuilderPreview Preview(CollectionRule rule)
    {
        if (!CollectionBuilderCatalog.TryGet(rule.Builder, out var builder))
            throw new ArgumentException($"Unknown collection builder '{rule.Builder}'.", nameof(rule));

        var warnings = new List<string>();
        var items = builder.Name.ToLowerInvariant() switch
        {
            "tmdb_movie" => PreviewTmdbMovies(rule, warnings),
            "tmdb_show" => PreviewTmdbShows(rule, warnings),
            "tmdb_collection" => PreviewTmdbCollections(rule, warnings),
            "imdb_id" => PreviewImdbTitles(rule, warnings),
            "tvdb_movie" => PreviewTvdbMovies(rule, warnings),
            "tvdb_show" => PreviewTvdbShows(rule, warnings),
            _ => UnsupportedPreview(builder, warnings),
        };

        return new()
        {
            Builder = builder,
            Items = Deduplicate(items),
            Warnings = warnings,
        };
    }

    private static IReadOnlyList<CollectionBuilderPreviewItem> PreviewTmdbMovies(CollectionRule rule, List<string> warnings)
    {
        var items = new List<CollectionBuilderPreviewItem>();
        foreach (var id in GetPositiveIntegerValues(rule, warnings))
        {
            var movie = RepoFactory.TMDB_Movie.GetByTmdbMovieID(id);
            items.Add(new()
            {
                ExternalID = ExternalMediaId.TmdbMovie(id),
                Title = movie?.EnglishTitle ?? $"TMDB Movie {id}",
                Summary = movie?.EnglishOverview,
            });
        }

        return items;
    }

    private static IReadOnlyList<CollectionBuilderPreviewItem> PreviewTmdbShows(CollectionRule rule, List<string> warnings)
    {
        var items = new List<CollectionBuilderPreviewItem>();
        foreach (var id in GetPositiveIntegerValues(rule, warnings))
        {
            var show = RepoFactory.TMDB_Show.GetByTmdbShowID(id);
            items.Add(new()
            {
                ExternalID = ExternalMediaId.TmdbShow(id),
                Title = show?.EnglishTitle ?? $"TMDB Show {id}",
                Summary = show?.EnglishOverview,
            });
        }

        return items;
    }

    private static IReadOnlyList<CollectionBuilderPreviewItem> PreviewTmdbCollections(CollectionRule rule, List<string> warnings)
    {
        var items = new List<CollectionBuilderPreviewItem>();
        foreach (var id in GetPositiveIntegerValues(rule, warnings))
        {
            var collection = RepoFactory.TMDB_Collection.GetByTmdbCollectionID(id);
            if (collection is null)
            {
                warnings.Add($"TMDB collection {id} is not cached locally yet.");
                continue;
            }

            foreach (var movie in collection.GetTmdbMovies())
            {
                items.Add(new()
                {
                    ExternalID = ExternalMediaId.TmdbMovie(movie.TmdbMovieID),
                    Title = movie.EnglishTitle,
                    Summary = movie.EnglishOverview,
                });
            }
        }

        return items;
    }

    private static IReadOnlyList<CollectionBuilderPreviewItem> PreviewImdbTitles(CollectionRule rule, List<string> warnings)
    {
        var kind = rule.Kind is MediaKind.Unknown ? MediaKind.Movie : rule.Kind;
        if (kind is not MediaKind.Movie and not MediaKind.Show)
        {
            warnings.Add($"IMDb title previews support Movie or Show kinds. Defaulting unsupported kind '{kind}' to Movie.");
            kind = MediaKind.Movie;
        }

        return GetStringValues(rule, warnings)
            .Select(value => new CollectionBuilderPreviewItem
            {
                ExternalID = ExternalMediaId.ImdbTitle(value, kind),
                Title = value,
            })
            .ToList();
    }

    private static IReadOnlyList<CollectionBuilderPreviewItem> PreviewTvdbMovies(CollectionRule rule, List<string> warnings) =>
        GetPositiveIntegerValues(rule, warnings)
            .Select(id => new CollectionBuilderPreviewItem
            {
                ExternalID = ExternalMediaId.TvdbMovie(id),
                Title = $"TVDB Movie {id}",
            })
            .ToList();

    private static IReadOnlyList<CollectionBuilderPreviewItem> PreviewTvdbShows(CollectionRule rule, List<string> warnings) =>
        GetPositiveIntegerValues(rule, warnings)
            .Select(id => new CollectionBuilderPreviewItem
            {
                ExternalID = ExternalMediaId.TvdbShow(id),
                Title = $"TVDB Show {id}",
            })
            .ToList();

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
            if (!int.TryParse(value, out var id) || id <= 0)
            {
                warnings.Add($"Ignoring invalid provider ID '{value}'.");
                continue;
            }

            values.Add(id);
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
}
