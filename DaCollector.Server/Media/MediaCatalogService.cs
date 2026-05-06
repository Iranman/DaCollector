using System;
using System.Collections.Generic;
using System.Linq;
using DaCollector.Abstractions.Metadata.Enums;
using DaCollector.Server.API.v3.Models.MediaCatalog;
using DaCollector.Server.Repositories;
using DaCollector.Server.Utilities;

#nullable enable
namespace DaCollector.Server.Media;

/// <summary>
/// Builds DaCollector's generic movie and TV catalog from cached provider metadata.
/// </summary>
public class MediaCatalogService
{
    public IReadOnlyList<MediaCatalogItem> GetItems(
        MediaKind kind,
        string? search,
        bool fuzzy,
        bool includeRestricted,
        bool includeVideos
    )
    {
        var items = kind switch
        {
            MediaKind.Unknown => GetMovies(includeRestricted, includeVideos).Concat(GetShows(includeRestricted)),
            MediaKind.Movie => GetMovies(includeRestricted, includeVideos),
            MediaKind.Show => GetShows(includeRestricted),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Media catalog supports Movie, Show, or Unknown for all items."),
        };

        if (string.IsNullOrWhiteSpace(search))
            return items
                .OrderBy(item => item.Title)
                .ThenBy(item => item.Kind)
                .ThenBy(item => item.ExternalID.Value)
                .ToList();

        return items
            .AsParallel()
            .Search(search, GetSearchTerms, fuzzy)
            .Select(result => result.Result)
            .ToList();
    }

    private static IEnumerable<MediaCatalogItem> GetMovies(bool includeRestricted, bool includeVideos) =>
        RepoFactory.TMDB_Movie
            .GetAll()
            .Where(movie => includeRestricted || !movie.IsRestricted)
            .Where(movie => includeVideos || !movie.IsVideo)
            .Select(MediaCatalogItem.FromMovie);

    private static IEnumerable<MediaCatalogItem> GetShows(bool includeRestricted) =>
        RepoFactory.TMDB_Show
            .GetAll()
            .Where(show => includeRestricted || !show.IsRestricted)
            .Select(MediaCatalogItem.FromShow);

    private static IEnumerable<string> GetSearchTerms(MediaCatalogItem item)
    {
        yield return item.Title;
        yield return item.OriginalTitle;
        yield return item.ExternalID.Value;

        foreach (var externalID in item.ExternalIDs)
            yield return externalID.Value;
    }
}
