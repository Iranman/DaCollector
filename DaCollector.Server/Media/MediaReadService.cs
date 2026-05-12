using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DaCollector.Server.API.v3.Models.Media;
using DaCollector.Server.Models.Internal;
using DaCollector.Server.Repositories;
using DaCollector.Server.Repositories.Direct;

#nullable enable
namespace DaCollector.Server.Media;

public class MediaReadService(MediaFileReviewStateRepository reviewStateRepository)
{
    public IReadOnlyList<MediaMovieDto> GetMovies(string provider, string? search, bool includeRestricted, bool includeVideos)
    {
        var providers = GetProviders(provider);
        var movies = new List<MediaMovieDto>();

        if (providers.Contains("tmdb"))
        {
            movies.AddRange(RepoFactory.TMDB_Movie.GetAll()
                .Where(movie => includeRestricted || !movie.IsRestricted)
                .Where(movie => includeVideos || !movie.IsVideo)
                .Select(MediaMovieDto.FromTmdbMovie));
        }

        if (providers.Contains("tvdb"))
            movies.AddRange(RepoFactory.TVDB_Movie.GetAll().Select(MediaMovieDto.FromTvdbMovie));

        return ApplySearch(movies, search, movie => [movie.Title, movie.OriginalTitle, .. movie.ExternalIDs.Select(id => id.Value)])
            .OrderBy(movie => movie.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(movie => movie.Provider)
            .ThenBy(movie => movie.ProviderID)
            .ToList();
    }

    public MediaMovieDto? GetMovie(string provider, int providerID)
        => NormalizeProvider(provider) switch
        {
            "tmdb" => RepoFactory.TMDB_Movie.GetByTmdbMovieID(providerID) is { } movie ? MediaMovieDto.FromTmdbMovie(movie) : null,
            "tvdb" => RepoFactory.TVDB_Movie.GetByTvdbMovieID(providerID) is { } movie ? MediaMovieDto.FromTvdbMovie(movie) : null,
            _ => null,
        };

    public IReadOnlyList<MediaShowDto> GetShows(string provider, string? search, bool includeRestricted)
    {
        var providers = GetProviders(provider);
        var shows = new List<MediaShowDto>();

        if (providers.Contains("tmdb"))
        {
            shows.AddRange(RepoFactory.TMDB_Show.GetAll()
                .Where(show => includeRestricted || !show.IsRestricted)
                .Select(MediaShowDto.FromTmdbShow));
        }

        if (providers.Contains("tvdb"))
            shows.AddRange(RepoFactory.TVDB_Show.GetAll().Select(MediaShowDto.FromTvdbShow));

        return ApplySearch(shows, search, show => [show.Title, show.OriginalTitle, .. show.ExternalIDs.Select(id => id.Value)])
            .OrderBy(show => show.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(show => show.Provider)
            .ThenBy(show => show.ProviderID)
            .ToList();
    }

    public MediaShowDto? GetShow(string provider, int providerID)
        => NormalizeProvider(provider) switch
        {
            "tmdb" => RepoFactory.TMDB_Show.GetByTmdbShowID(providerID) is { } show ? MediaShowDto.FromTmdbShow(show) : null,
            "tvdb" => RepoFactory.TVDB_Show.GetByTvdbShowID(providerID) is { } show ? MediaShowDto.FromTvdbShow(show) : null,
            _ => null,
        };

    public IReadOnlyList<MediaSeasonDto>? GetShowSeasons(string provider, int providerID)
        => NormalizeProvider(provider) switch
        {
            "tmdb" when RepoFactory.TMDB_Show.GetByTmdbShowID(providerID) is not null => RepoFactory.TMDB_Season
                .GetByTmdbShowID(providerID)
                .Select(MediaSeasonDto.FromTmdbSeason)
                .ToList(),
            "tvdb" when RepoFactory.TVDB_Show.GetByTvdbShowID(providerID) is not null => RepoFactory.TVDB_Season
                .GetByTvdbShowID(providerID)
                .OrderBy(season => season.SeasonNumber == 0)
                .ThenBy(season => season.SeasonNumber)
                .Select(MediaSeasonDto.FromTvdbSeason)
                .ToList(),
            _ => null,
        };

    public IReadOnlyList<MediaEpisodeDto>? GetShowEpisodes(string provider, int providerID, int? seasonNumber)
        => NormalizeProvider(provider) switch
        {
            "tmdb" when RepoFactory.TMDB_Show.GetByTmdbShowID(providerID) is not null => RepoFactory.TMDB_Episode
                .GetByTmdbShowID(providerID)
                .Where(episode => !seasonNumber.HasValue || episode.SeasonNumber == seasonNumber.Value)
                .Select(MediaEpisodeDto.FromTmdbEpisode)
                .ToList(),
            "tvdb" when RepoFactory.TVDB_Show.GetByTvdbShowID(providerID) is not null => RepoFactory.TVDB_Episode
                .GetByTvdbShowID(providerID)
                .Where(episode => !seasonNumber.HasValue || episode.SeasonNumber == seasonNumber.Value)
                .OrderBy(episode => episode.SeasonNumber == 0)
                .ThenBy(episode => episode.SeasonNumber)
                .ThenBy(episode => episode.EpisodeNumber)
                .Select(MediaEpisodeDto.FromTvdbEpisode)
                .ToList(),
            _ => null,
        };

    public IReadOnlyList<MediaFileDto> GetFiles(string? search, bool includeIgnored, bool includeReview, bool includeAbsolutePaths)
    {
        var files = RepoFactory.VideoLocal.GetAll()
            .Where(file => includeIgnored || !file.IsIgnored)
            .Select(file => MediaFileDto.FromVideoLocal(file, includeReview ? GetReview(file.VideoLocalID) : null, includeAbsolutePaths));

        return ApplySearch(files, search, file =>
            [
                file.FileID.ToString(),
                .. file.Hashes.Select(hash => hash.Value),
                .. file.Locations.Select(location => location.RelativePath),
                .. file.Locations.Select(location => location.AbsolutePath ?? string.Empty),
            ])
            .OrderBy(file => file.Locations.FirstOrDefault()?.RelativePath ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(file => file.FileID)
            .ToList();
    }

    public MediaFileDto? GetFile(int fileID, bool includeReview, bool includeAbsolutePaths)
        => RepoFactory.VideoLocal.GetByID(fileID) is { } file
            ? MediaFileDto.FromVideoLocal(file, includeReview ? GetReview(file.VideoLocalID) : null, includeAbsolutePaths)
            : null;

    public IReadOnlyList<MediaFileDto> GetFilesByPathEndsWith(string tail, bool includeReview, bool includeAbsolutePaths)
    {
        var normalized = tail
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);

        var files = RepoFactory.VideoLocalPlace.GetAll()
            .Where(place => place.Path?.EndsWith(normalized, StringComparison.OrdinalIgnoreCase) == true
                         || place.RelativePath.EndsWith(normalized, StringComparison.OrdinalIgnoreCase))
            .Select(place => place.VideoLocal)
            .Where(file => file is not null)
            .Select(file => file!)
            .DistinctBy(file => file.VideoLocalID)
            .ToList();

        var reviews = includeReview
            ? reviewStateRepository.GetByVideoLocalIDs(files.Select(f => f.VideoLocalID))
                .ToDictionary(r => r.VideoLocalID, MediaFileReviewStateDto.FromState)
            : null;

        return files
            .Select(file => MediaFileDto.FromVideoLocal(
                file, reviews?.GetValueOrDefault(file.VideoLocalID), includeAbsolutePaths))
            .ToList();
    }

    public static bool IsValidProvider(string provider, bool allowAll)
    {
        var normalized = NormalizeProvider(provider);
        return normalized is "tmdb" or "tvdb" || allowAll && normalized == "all";
    }

    private MediaFileReviewStateDto? GetReview(int videoLocalID)
        => reviewStateRepository.GetByVideoLocalID(videoLocalID) is MediaFileReviewState state
            ? MediaFileReviewStateDto.FromState(state)
            : null;

    private static IReadOnlySet<string> GetProviders(string provider)
        => NormalizeProvider(provider) switch
        {
            "tmdb" => new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "tmdb" },
            "tvdb" => new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "tvdb" },
            _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "tmdb", "tvdb" },
        };

    private static IEnumerable<T> ApplySearch<T>(IEnumerable<T> items, string? search, Func<T, IEnumerable<string?>> terms)
    {
        if (string.IsNullOrWhiteSpace(search))
            return items;

        var query = search.Trim();
        return items.Where(item => terms(item).Any(term => term?.Contains(query, StringComparison.OrdinalIgnoreCase) == true));
    }

    private static string NormalizeProvider(string provider)
        => provider.Trim().ToLowerInvariant();
}
