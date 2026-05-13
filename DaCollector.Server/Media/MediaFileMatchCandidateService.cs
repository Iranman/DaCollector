using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using DaCollector.Server.Models.Internal;
using DaCollector.Server.Parsing;
using DaCollector.Server.Providers.TMDB;
using DaCollector.Server.Providers.TVDB;
using DaCollector.Server.Repositories;
using DaCollector.Server.Repositories.Direct;

#nullable enable
namespace DaCollector.Server.Media;

public class MediaFileMatchCandidateService(
    ILogger<MediaFileMatchCandidateService> logger,
    MediaFileReviewService reviewService,
    TmdbSearchService tmdbSearchService,
    TmdbMetadataService tmdbMetadataService,
    TvdbMetadataService tvdbMetadataService,
    MediaFileReviewStateRepository reviewStateRepository,
    MediaFileMatchCandidateRepository candidateRepository
)
{
    private const double MinScore = 0.5;
    internal const double AutoMatchThreshold = 0.92;

    public Task<MediaFileMatchScanResult> ScanFileAsync(
        int videoLocalID,
        bool refreshParse = false,
        bool includeOnlineSearch = false,
        bool refreshExplicitIds = false
    )
        => ScanFileCoreAsync(videoLocalID, refreshParse, includeOnlineSearch, refreshExplicitIds);

    public async Task<MediaFileMatchBatchScanResult> ScanUnmatchedFilesAsync(
        bool includeIgnored = false,
        bool refreshParse = false,
        bool includeOnlineSearch = false,
        bool refreshExplicitIds = false
    )
    {
        var videos = RepoFactory.VideoLocal
            .GetVideosWithoutEpisode(includeBrokenXRefs: true)
            .Where(video => includeIgnored || !video.IsIgnored)
            .OrderBy(video => video.FirstValidPlace?.Path ?? video.FirstValidPlace?.RelativePath ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (includeIgnored)
        {
            var existingIDs = videos.Select(video => video.VideoLocalID).ToHashSet();
            videos.AddRange(RepoFactory.VideoLocal.GetIgnoredVideos().Where(video => existingIDs.Add(video.VideoLocalID)));
        }

        var results = new List<MediaFileMatchScanResult>();
        foreach (var video in videos)
            results.Add(await ScanFileCoreAsync(video.VideoLocalID, refreshParse, includeOnlineSearch, refreshExplicitIds).ConfigureAwait(false));

        return new MediaFileMatchBatchScanResult
        {
            IncludeIgnored = includeIgnored,
            RefreshParse = refreshParse,
            IncludeOnlineSearch = includeOnlineSearch,
            RefreshExplicitIds = refreshExplicitIds,
            ScannedFileCount = results.Count(result => result.Scanned),
            CandidateCount = results.Sum(result => result.CandidateCount),
            Results = results,
        };
    }

    public IReadOnlyList<MediaFileMatchCandidate> GetPendingCandidates()
        => candidateRepository.GetByStatus("Pending");

    public IReadOnlyList<MediaFileMatchCandidate> GetCandidatesForFile(int videoLocalID)
        => candidateRepository.GetByVideoLocalID(videoLocalID);

    public MediaFileReviewItem? ApproveCandidate(int candidateID)
    {
        var candidate = candidateRepository.GetByID(candidateID);
        if (candidate is null || candidate.Status != "Pending")
            return null;

        var item = reviewService.SetManualMatch(candidate.VideoLocalID, new ManualFileMatchRequest
        {
            EntityType = candidate.ProviderType == "show" ? "Show" : "Movie",
            Provider = candidate.Provider,
            ProviderID = candidate.ProviderItemID.ToString(CultureInfo.InvariantCulture),
            Title = candidate.Title,
            Locked = true,
        });
        if (item is null)
            return null;

        var now = DateTime.UtcNow;
        candidate.Status = "Approved";
        candidate.ReviewedAt = now;
        candidate.UpdatedAt = now;

        var siblings = candidateRepository.GetByVideoLocalID(candidate.VideoLocalID)
            .Where(c => c.MediaFileMatchCandidateID != candidateID && c.Status == "Pending")
            .ToList();
        foreach (var sibling in siblings)
        {
            sibling.Status = "Rejected";
            sibling.ReviewedAt = now;
            sibling.UpdatedAt = now;
        }

        var allUpdates = new List<MediaFileMatchCandidate>(siblings.Count + 1) { candidate };
        allUpdates.AddRange(siblings);
        candidateRepository.Save(allUpdates);

        logger.LogInformation("Approved file candidate {CandidateID}: {Provider}/{Type} ID {ProviderItemID} for file {VideoLocalID}.",
            candidateID, candidate.Provider, candidate.ProviderType, candidate.ProviderItemID, candidate.VideoLocalID);

        return item;
    }

    public bool RejectCandidate(int candidateID)
    {
        var candidate = candidateRepository.GetByID(candidateID);
        if (candidate is null || candidate.Status != "Pending")
            return false;

        candidate.Status = "Rejected";
        candidate.ReviewedAt = DateTime.UtcNow;
        candidate.UpdatedAt = candidate.ReviewedAt.Value;
        candidateRepository.Save(candidate);
        return true;
    }

    private async Task<MediaFileMatchScanResult> ScanFileCoreAsync(
        int videoLocalID,
        bool refreshParse,
        bool includeOnlineSearch,
        bool refreshExplicitIds
    )
    {
        if (reviewService.GetFileReview(videoLocalID, refreshParse) is null)
            return new()
            {
                VideoLocalID = videoLocalID,
                QueryTitle = null,
                QueryYear = null,
                ParsedKind = null,
                IncludeOnlineSearch = includeOnlineSearch,
                RefreshExplicitIds = refreshExplicitIds,
                CandidateCount = 0,
                Scanned = false,
                Message = "VideoLocal was not found.",
            };

        var state = reviewStateRepository.GetByVideoLocalID(videoLocalID);
        if (state is null)
            return new()
            {
                VideoLocalID = videoLocalID,
                QueryTitle = null,
                QueryYear = null,
                ParsedKind = null,
                IncludeOnlineSearch = includeOnlineSearch,
                RefreshExplicitIds = refreshExplicitIds,
                CandidateCount = 0,
                Scanned = false,
                Message = "Media file review state was not found.",
            };

        var queryTitle = GetQueryTitle(state);
        var queryYear = GetQueryYear(state);
        if (string.IsNullOrWhiteSpace(queryTitle))
            return new()
            {
                VideoLocalID = videoLocalID,
                QueryTitle = null,
                QueryYear = queryYear,
                ParsedKind = state.ParsedKind,
                IncludeOnlineSearch = includeOnlineSearch,
                RefreshExplicitIds = refreshExplicitIds,
                CandidateCount = 0,
                Scanned = false,
                Message = "Parser did not produce a searchable title.",
            };

        var videoPath = RepoFactory.VideoLocal.GetByID(videoLocalID)?.FirstValidPlace?.Path;
        var nfo = NfoSidecarParser.TryParse(videoPath);

        var candidates = await BuildCandidatesAsync(state, queryTitle, queryYear, nfo, includeOnlineSearch, refreshExplicitIds).ConfigureAwait(false);
        if (candidates.Count > 0)
        {
            candidateRepository.Save(candidates);

            if (state.Status == MediaFileReviewStatus.Pending)
            {
                var topCandidate = candidates.MaxBy(c => c.ConfidenceScore);
                if (topCandidate is not null && topCandidate.ConfidenceScore >= AutoMatchThreshold)
                {
                    var highConfidenceCount = candidates.Count(c => c.ConfidenceScore >= AutoMatchThreshold);
                    if (highConfidenceCount == 1)
                    {
                        ApproveCandidate(topCandidate.MediaFileMatchCandidateID);
                        logger.LogInformation("Auto-matched file {VideoLocalID} to {Provider}/{Type} ID {ProviderItemID} (score {Score:F4}).",
                            videoLocalID, topCandidate.Provider, topCandidate.ProviderType, topCandidate.ProviderItemID, topCandidate.ConfidenceScore);
                    }
                }
            }
        }

        logger.LogInformation("Scanned file {VideoLocalID} ({Title}): {Count} candidate(s) added or refreshed.",
            videoLocalID, queryTitle, candidates.Count);

        return new()
        {
            VideoLocalID = videoLocalID,
            QueryTitle = queryTitle,
            QueryYear = queryYear,
            ParsedKind = state.ParsedKind,
            IncludeOnlineSearch = includeOnlineSearch,
            RefreshExplicitIds = refreshExplicitIds,
            CandidateCount = candidates.Count,
            Scanned = true,
            Message = candidates.Count == 0
                ? "No provider match candidates found."
                : includeOnlineSearch
                    ? "Provider match candidates added or refreshed from cached and online sources."
                    : "Provider match candidates added or refreshed from cached sources.",
        };
    }

    private async Task<IReadOnlyList<MediaFileMatchCandidate>> BuildCandidatesAsync(
        MediaFileReviewState state,
        string queryTitle,
        int? queryYear,
        NfoSidecarData? nfo,
        bool includeOnlineSearch,
        bool refreshExplicitIds
    )
    {
        var candidates = new List<MediaFileMatchCandidate>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queryNormalized = MediaFileMatchCandidateScoring.NormalizeTitle(queryTitle);
        var queryWords = queryNormalized.Length > 0
            ? queryNormalized.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
        var targetTypes = GetTargetProviderTypes(state.ParsedKind).ToList();

        await AddExplicitIdCandidatesAsync(state, queryTitle, queryYear, targetTypes, refreshExplicitIds, candidates, seen).ConfigureAwait(false);

        if (nfo is not null)
            AddNfoCandidates(nfo, state.VideoLocalID, queryTitle, queryYear, candidates, seen);

        if (targetTypes.Contains("movie"))
        {
            foreach (var movie in RepoFactory.TMDB_Movie.GetAll())
            {
                var displayTitle = movie.EnglishTitle.Length > 0 ? movie.EnglishTitle : movie.OriginalTitle;
                TryAddScoredCandidate(state.VideoLocalID, "tmdb", movie.TmdbMovieID, "movie", displayTitle,
                    movie.ReleasedAt?.Year, queryTitle, queryYear, queryWords, queryNormalized, candidates, seen,
                    queryRuntimeMinutes: nfo?.RuntimeMinutes, candidateRuntimeMinutes: movie.RuntimeMinutes);
            }

            foreach (var movie in RepoFactory.TVDB_Movie.GetAll())
            {
                TryAddScoredCandidate(state.VideoLocalID, "tvdb", movie.TvdbMovieID, "movie", movie.Name,
                    movie.Year, queryTitle, queryYear, queryWords, queryNormalized, candidates, seen,
                    queryRuntimeMinutes: nfo?.RuntimeMinutes, candidateRuntimeMinutes: movie.RuntimeMinutes);
            }
        }

        if (targetTypes.Contains("show"))
        {
            foreach (var show in RepoFactory.TMDB_Show.GetAll())
            {
                var displayTitle = show.EnglishTitle.Length > 0 ? show.EnglishTitle : show.OriginalTitle;
                TryAddScoredCandidate(state.VideoLocalID, "tmdb", show.TmdbShowID, "show", displayTitle,
                    show.FirstAiredAt?.Year, queryTitle, queryYear, queryWords, queryNormalized, candidates, seen);
            }

            foreach (var show in RepoFactory.TVDB_Show.GetAll())
            {
                TryAddScoredCandidate(state.VideoLocalID, "tvdb", show.TvdbShowID, "show", show.Name,
                    show.Year, queryTitle, queryYear, queryWords, queryNormalized, candidates, seen);
            }
        }

        if (includeOnlineSearch)
            await AddOnlineTmdbCandidatesAsync(state.VideoLocalID, targetTypes, queryTitle, queryYear, queryWords, queryNormalized, candidates, seen).ConfigureAwait(false);

        return candidates;
    }

    private void AddNfoCandidates(
        NfoSidecarData nfo,
        int videoLocalID,
        string queryTitle,
        int? queryYear,
        List<MediaFileMatchCandidate> candidates,
        HashSet<string> seen
    )
    {
        if (string.IsNullOrWhiteSpace(nfo.ImdbId))
            return;

        foreach (var movie in RepoFactory.TMDB_Movie.GetAll()
                     .Where(m => string.Equals(m.ImdbMovieID, nfo.ImdbId, StringComparison.OrdinalIgnoreCase)))
        {
            var title = movie.EnglishTitle.Length > 0 ? movie.EnglishTitle : movie.OriginalTitle;
            AddDirectCandidate(videoLocalID, "tmdb", movie.TmdbMovieID, "movie", title, movie.ReleasedAt?.Year,
                $"NFO sidecar IMDb ID {nfo.ImdbId} matched cached TMDB movie", candidates, seen);
        }
    }

    private async Task AddExplicitIdCandidatesAsync(
        MediaFileReviewState state,
        string queryTitle,
        int? queryYear,
        IReadOnlyCollection<string> targetTypes,
        bool refreshExplicitIds,
        List<MediaFileMatchCandidate> candidates,
        HashSet<string> seen
    )
    {
        foreach (var externalId in state.ParsedExternalIds)
        {
            if (externalId.Source.Equals("TMDB", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(externalId.Id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var tmdbId))
            {
                foreach (var targetType in targetTypes)
                {
                    if (refreshExplicitIds)
                        await RefreshExplicitProviderId("tmdb", tmdbId, targetType).ConfigureAwait(false);

                    AddDirectCandidate(state.VideoLocalID, "tmdb", tmdbId, targetType, queryTitle, queryYear,
                        $"Explicit TMDB ID {tmdbId} in path", candidates, seen);
                }
            }
            else if (externalId.Source.Equals("TVDB", StringComparison.OrdinalIgnoreCase)
                     && int.TryParse(externalId.Id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var tvdbId))
            {
                foreach (var targetType in targetTypes)
                {
                    if (refreshExplicitIds)
                        await RefreshExplicitProviderId("tvdb", tvdbId, targetType).ConfigureAwait(false);

                    AddDirectCandidate(state.VideoLocalID, "tvdb", tvdbId, targetType, queryTitle, queryYear,
                        $"Explicit TVDB ID {tvdbId} in path", candidates, seen);
                }
            }
            else if (externalId.Source.Equals("IMDb", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var movie in RepoFactory.TMDB_Movie.GetAll()
                             .Where(movie => string.Equals(movie.ImdbMovieID, externalId.Id, StringComparison.OrdinalIgnoreCase)))
                {
                    var title = movie.EnglishTitle.Length > 0 ? movie.EnglishTitle : movie.OriginalTitle;
                    AddDirectCandidate(state.VideoLocalID, "tmdb", movie.TmdbMovieID, "movie", title, movie.ReleasedAt?.Year,
                        $"Explicit IMDb ID {externalId.Id} matched cached TMDB movie", candidates, seen);
                }
            }
        }
    }

    private async Task AddOnlineTmdbCandidatesAsync(
        int videoLocalID,
        IReadOnlyCollection<string> targetTypes,
        string queryTitle,
        int? queryYear,
        HashSet<string> queryWords,
        string queryNormalized,
        List<MediaFileMatchCandidate> candidates,
        HashSet<string> seen
    )
    {
        var year = queryYear.GetValueOrDefault();

        if (targetTypes.Contains("movie"))
        {
            try
            {
                var (movies, _) = await tmdbSearchService.SearchMovies(queryTitle, includeRestricted: false, year: year, page: 1, pageSize: 6).ConfigureAwait(false);
                foreach (var movie in movies)
                {
                    var title = string.IsNullOrWhiteSpace(movie.Title) ? movie.OriginalTitle : movie.Title;
                    TryAddScoredCandidate(videoLocalID, "tmdb", movie.ID, "movie", title, movie.ReleasedAt?.Year,
                        queryTitle, queryYear, queryWords, queryNormalized, candidates, seen, "Online TMDB movie search");
                    if (!string.Equals(title, movie.OriginalTitle, StringComparison.OrdinalIgnoreCase))
                        TryAddScoredCandidate(videoLocalID, "tmdb", movie.ID, "movie", movie.OriginalTitle, movie.ReleasedAt?.Year,
                            queryTitle, queryYear, queryWords, queryNormalized, candidates, seen, "Online TMDB movie search");
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Online TMDB movie search failed for unmatched file query '{QueryTitle}'.", queryTitle);
            }
        }

        if (targetTypes.Contains("show"))
        {
            try
            {
                var (shows, _) = await tmdbSearchService.SearchShows(queryTitle, includeRestricted: false, year: year, page: 1, pageSize: 6).ConfigureAwait(false);
                foreach (var show in shows)
                {
                    var title = string.IsNullOrWhiteSpace(show.Title) ? show.OriginalTitle : show.Title;
                    TryAddScoredCandidate(videoLocalID, "tmdb", show.ID, "show", title, show.FirstAiredAt?.Year,
                        queryTitle, queryYear, queryWords, queryNormalized, candidates, seen, "Online TMDB show search");
                    if (!string.Equals(title, show.OriginalTitle, StringComparison.OrdinalIgnoreCase))
                        TryAddScoredCandidate(videoLocalID, "tmdb", show.ID, "show", show.OriginalTitle, show.FirstAiredAt?.Year,
                            queryTitle, queryYear, queryWords, queryNormalized, candidates, seen, "Online TMDB show search");
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Online TMDB show search failed for unmatched file query '{QueryTitle}'.", queryTitle);
            }
        }
    }

    private async Task RefreshExplicitProviderId(string provider, int providerItemID, string providerType)
    {
        try
        {
            switch (provider, providerType)
            {
                case ("tmdb", "movie") when RepoFactory.TMDB_Movie.GetByTmdbMovieID(providerItemID) is null:
                    await tmdbMetadataService.UpdateMovie(providerItemID).ConfigureAwait(false);
                    break;
                case ("tmdb", "show") when RepoFactory.TMDB_Show.GetByTmdbShowID(providerItemID) is null:
                    await tmdbMetadataService.UpdateShow(providerItemID, quickRefresh: true).ConfigureAwait(false);
                    break;
                case ("tvdb", "movie") when RepoFactory.TVDB_Movie.GetByTvdbMovieID(providerItemID) is null:
                    await tvdbMetadataService.UpdateMovie(providerItemID).ConfigureAwait(false);
                    break;
                case ("tvdb", "show") when RepoFactory.TVDB_Show.GetByTvdbShowID(providerItemID) is null:
                    await tvdbMetadataService.UpdateShow(providerItemID).ConfigureAwait(false);
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to refresh explicit {Provider}/{ProviderType} ID {ProviderItemID}.",
                provider, providerType, providerItemID);
        }
    }

    private void TryAddScoredCandidate(
        int videoLocalID,
        string provider,
        int providerItemID,
        string providerType,
        string title,
        int? year,
        string queryTitle,
        int? queryYear,
        HashSet<string> queryWords,
        string queryNormalized,
        List<MediaFileMatchCandidate> candidates,
        HashSet<string> seen,
        string? sourceReason = null,
        int? queryRuntimeMinutes = null,
        int? candidateRuntimeMinutes = null
    )
    {
        if (!MediaFileMatchCandidateScoring.PassesTitleFilter(queryWords, queryNormalized, title))
            return;

        var reasons = new List<string>();
        var score = MediaFileMatchCandidateScoring.ComputeScore(queryTitle, queryYear, title, year, reasons, queryRuntimeMinutes, candidateRuntimeMinutes);
        if (score < MinScore)
            return;
        if (!string.IsNullOrWhiteSpace(sourceReason))
            reasons.Insert(0, sourceReason);

        AddCandidate(videoLocalID, provider, providerItemID, providerType, title, year, score, reasons, candidates, seen);
    }

    private void AddDirectCandidate(
        int videoLocalID,
        string provider,
        int providerItemID,
        string providerType,
        string fallbackTitle,
        int? fallbackYear,
        string reason,
        List<MediaFileMatchCandidate> candidates,
        HashSet<string> seen
    )
    {
        var (title, year) = GetCachedTitleAndYear(provider, providerItemID, providerType);
        AddCandidate(videoLocalID, provider, providerItemID, providerType, title ?? fallbackTitle, year ?? fallbackYear, 1.0,
            [reason], candidates, seen);
    }

    private void AddCandidate(
        int videoLocalID,
        string provider,
        int providerItemID,
        string providerType,
        string title,
        int? year,
        double score,
        List<string> reasons,
        List<MediaFileMatchCandidate> candidates,
        HashSet<string> seen
    )
    {
        var key = $"{videoLocalID}|{provider}|{providerItemID}|{providerType}";
        if (!seen.Add(key))
            return;

        var existing = candidateRepository.GetByFileAndProvider(videoLocalID, provider, providerItemID, providerType);
        if (existing is { Status: "Approved" or "Rejected" })
            return;

        candidates.Add(UpsertCandidate(existing, videoLocalID, provider, providerItemID, providerType, title, year, score, reasons));
    }

    private static MediaFileMatchCandidate UpsertCandidate(
        MediaFileMatchCandidate? existing,
        int videoLocalID,
        string provider,
        int providerItemID,
        string providerType,
        string title,
        int? year,
        double score,
        List<string> reasons
    )
    {
        var now = DateTime.UtcNow;
        if (existing is not null)
        {
            existing.Title = title;
            existing.Year = year;
            existing.ConfidenceScore = Math.Round(score, 4);
            existing.ReasonsJson = JsonSerializer.Serialize(reasons);
            existing.Status = "Pending";
            existing.ReviewedAt = null;
            existing.UpdatedAt = now;
            return existing;
        }

        return new()
        {
            VideoLocalID = videoLocalID,
            Provider = provider,
            ProviderItemID = providerItemID,
            ProviderType = providerType,
            Title = title,
            Year = year,
            ConfidenceScore = Math.Round(score, 4),
            ReasonsJson = JsonSerializer.Serialize(reasons),
            Status = "Pending",
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    private static string? GetQueryTitle(MediaFileReviewState state)
        => state.ParsedKind switch
        {
            nameof(ParsedMediaKind.Movie) => state.ParsedTitle,
            nameof(ParsedMediaKind.TvEpisode) or nameof(ParsedMediaKind.MultiEpisodeTvFile) => state.ParsedShowTitle ?? state.ParsedTitle,
            _ => state.ParsedTitle ?? state.ParsedShowTitle,
        };

    private static int? GetQueryYear(MediaFileReviewState state)
    {
        if (state.ParsedYear.HasValue)
            return state.ParsedYear;
        if (DateOnly.TryParseExact(state.ParsedAirDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var airDate))
            return airDate.Year;
        return null;
    }

    private static IEnumerable<string> GetTargetProviderTypes(string parsedKind)
        => parsedKind switch
        {
            nameof(ParsedMediaKind.Movie) => ["movie"],
            nameof(ParsedMediaKind.TvEpisode) or nameof(ParsedMediaKind.MultiEpisodeTvFile) => ["show"],
            _ => ["movie", "show"],
        };

    private static (string? Title, int? Year) GetCachedTitleAndYear(string provider, int providerItemID, string providerType)
    {
        if (provider == "tmdb" && providerType == "movie" && RepoFactory.TMDB_Movie.GetByTmdbMovieID(providerItemID) is { } tmdbMovie)
            return (tmdbMovie.EnglishTitle.Length > 0 ? tmdbMovie.EnglishTitle : tmdbMovie.OriginalTitle, tmdbMovie.ReleasedAt?.Year);
        if (provider == "tmdb" && providerType == "show" && RepoFactory.TMDB_Show.GetByTmdbShowID(providerItemID) is { } tmdbShow)
            return (tmdbShow.EnglishTitle.Length > 0 ? tmdbShow.EnglishTitle : tmdbShow.OriginalTitle, tmdbShow.FirstAiredAt?.Year);
        if (provider == "tvdb" && providerType == "movie" && RepoFactory.TVDB_Movie.GetByTvdbMovieID(providerItemID) is { } tvdbMovie)
            return (tvdbMovie.Name, tvdbMovie.Year);
        if (provider == "tvdb" && providerType == "show" && RepoFactory.TVDB_Show.GetByTvdbShowID(providerItemID) is { } tvdbShow)
            return (tvdbShow.Name, tvdbShow.Year);
        return (null, null);
    }
}

public sealed record MediaFileMatchScanResult
{
    public required int VideoLocalID { get; init; }

    public required string? QueryTitle { get; init; }

    public required int? QueryYear { get; init; }

    public required string? ParsedKind { get; init; }

    public required bool IncludeOnlineSearch { get; init; }

    public required bool RefreshExplicitIds { get; init; }

    public required int CandidateCount { get; init; }

    public required bool Scanned { get; init; }

    public required string Message { get; init; }
}

public sealed record MediaFileMatchBatchScanResult
{
    public required bool IncludeIgnored { get; init; }

    public required bool RefreshParse { get; init; }

    public required bool IncludeOnlineSearch { get; init; }

    public required bool RefreshExplicitIds { get; init; }

    public required int ScannedFileCount { get; init; }

    public required int CandidateCount { get; init; }

    public IReadOnlyList<MediaFileMatchScanResult> Results { get; init; } = [];
}
