using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using DaCollector.Server.Models.Internal;
using DaCollector.Server.Repositories;

#nullable enable
namespace DaCollector.Server.Providers;

/// <summary>
/// Builds and manages the provider match candidate queue for unlinked or low-confidence MediaSeries entries.
/// Candidates are sourced from locally cached TMDB and TVDB data; no outbound API calls are made.
/// </summary>
public class ProviderMatchQueueService(ILogger<ProviderMatchQueueService> logger)
{
    private const double MinScore = 0.5;

    // ─────────────────────────── Scanning ────────────────────────────

    public Task ScanSeriesAsync(int mediaSeriesID)
        => Task.Run(() => ScanSeriesCore(mediaSeriesID));

    private void ScanSeriesCore(int mediaSeriesID)
    {
        var series = RepoFactory.MediaSeries.GetByID(mediaSeriesID);
        if (series is null)
            return;

        var queryTitle = series.Title;
        if (string.IsNullOrWhiteSpace(queryTitle))
            return;

        var queryYear = series.AniDB_Anime?.AirDate?.Year
            ?? series.EpisodeAddedDate?.Year;

        var queryNormalized = NormalizeTitle(queryTitle);
        var queryWords = queryNormalized.Length > 0
            ? queryNormalized.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);

        var candidates = new List<ProviderMatchCandidate>();

        // TMDB shows — skip if series already has a direct TMDB show link
        if (!series.TMDB_ShowID.HasValue)
        {
            foreach (var show in RepoFactory.TMDB_Show.GetAll())
            {
                if (!PassesTitleFilter(queryWords, queryNormalized, show.EnglishTitle) &&
                    (show.OriginalTitle.Length == 0 || !PassesTitleFilter(queryWords, queryNormalized, show.OriginalTitle)))
                    continue;

                var existing = RepoFactory.ProviderMatchCandidate.GetBySeriesAndProvider(mediaSeriesID, "tmdb", show.TmdbShowID, "show");
                if (existing is { Status: "Approved" or "Pending" })
                    continue;

                var reasons = new List<string>();
                var score = ComputeScore(queryTitle, queryYear, show.EnglishTitle, show.FirstAiredAt?.Year, reasons);
                if (score < MinScore && show.OriginalTitle.Length > 0)
                    score = Math.Max(score, ComputeScore(queryTitle, queryYear, show.OriginalTitle, show.FirstAiredAt?.Year, reasons));
                if (score < MinScore)
                    continue;

                var displayTitle = show.EnglishTitle.Length > 0 ? show.EnglishTitle : show.OriginalTitle;
                candidates.Add(UpsertCandidate(existing, mediaSeriesID, "tmdb", show.TmdbShowID, "show",
                    displayTitle, show.FirstAiredAt?.Year, score, reasons));
            }
        }

        // TMDB movies — skip if series already has a direct TMDB movie link
        if (!series.TMDB_MovieID.HasValue)
        {
            foreach (var movie in RepoFactory.TMDB_Movie.GetAll())
            {
                if (!PassesTitleFilter(queryWords, queryNormalized, movie.EnglishTitle) &&
                    (movie.OriginalTitle.Length == 0 || !PassesTitleFilter(queryWords, queryNormalized, movie.OriginalTitle)))
                    continue;

                var existing = RepoFactory.ProviderMatchCandidate.GetBySeriesAndProvider(mediaSeriesID, "tmdb", movie.TmdbMovieID, "movie");
                if (existing is { Status: "Approved" or "Pending" })
                    continue;

                var reasons = new List<string>();
                var score = ComputeScore(queryTitle, queryYear, movie.EnglishTitle, movie.ReleasedAt?.Year, reasons);
                if (score < MinScore && movie.OriginalTitle.Length > 0)
                    score = Math.Max(score, ComputeScore(queryTitle, queryYear, movie.OriginalTitle, movie.ReleasedAt?.Year, reasons));
                if (score < MinScore)
                    continue;

                var displayTitle = movie.EnglishTitle.Length > 0 ? movie.EnglishTitle : movie.OriginalTitle;
                candidates.Add(UpsertCandidate(existing, mediaSeriesID, "tmdb", movie.TmdbMovieID, "movie",
                    displayTitle, movie.ReleasedAt?.Year, score, reasons));
            }
        }

        // TVDB shows — skip if series already has a direct TVDB show link
        if (!series.TvdbShowExternalID.HasValue)
        {
            foreach (var show in RepoFactory.TVDB_Show.GetAll())
            {
                if (!PassesTitleFilter(queryWords, queryNormalized, show.Name))
                    continue;

                var existing = RepoFactory.ProviderMatchCandidate.GetBySeriesAndProvider(mediaSeriesID, "tvdb", show.TvdbShowID, "show");
                if (existing is { Status: "Approved" or "Pending" })
                    continue;

                var reasons = new List<string>();
                var score = ComputeScore(queryTitle, queryYear, show.Name, show.Year, reasons);
                if (score < MinScore)
                    continue;

                candidates.Add(UpsertCandidate(existing, mediaSeriesID, "tvdb", show.TvdbShowID, "show",
                    show.Name, show.Year, score, reasons));
            }
        }

        // TVDB movies — skip if series already has a direct TVDB movie link
        if (!series.TvdbMovieExternalID.HasValue)
        {
            foreach (var movie in RepoFactory.TVDB_Movie.GetAll())
            {
                if (!PassesTitleFilter(queryWords, queryNormalized, movie.Name))
                    continue;

                var existing = RepoFactory.ProviderMatchCandidate.GetBySeriesAndProvider(mediaSeriesID, "tvdb", movie.TvdbMovieID, "movie");
                if (existing is { Status: "Approved" or "Pending" })
                    continue;

                var reasons = new List<string>();
                var score = ComputeScore(queryTitle, queryYear, movie.Name, movie.Year, reasons);
                if (score < MinScore)
                    continue;

                candidates.Add(UpsertCandidate(existing, mediaSeriesID, "tvdb", movie.TvdbMovieID, "movie",
                    movie.Name, movie.Year, score, reasons));
            }
        }

        if (candidates.Count > 0)
            RepoFactory.ProviderMatchCandidate.Save(candidates);

        logger.LogInformation("Scanned series {MediaSeriesID} ({Title}): {Count} candidate(s) added or refreshed.",
            mediaSeriesID, queryTitle, candidates.Count);
    }

    // ─────────────────────────── Review ──────────────────────────────

    public bool ApproveCandidate(int candidateID)
    {
        var candidate = RepoFactory.ProviderMatchCandidate.GetByID(candidateID);
        if (candidate is null || candidate.Status != "Pending")
            return false;

        var series = RepoFactory.MediaSeries.GetByID(candidate.MediaSeriesID);
        if (series is null)
            return false;

        switch (candidate.Provider, candidate.ProviderType)
        {
            case ("tmdb", "show"):
                series.TMDB_ShowID = candidate.ProviderItemID;
                break;
            case ("tmdb", "movie"):
                series.TMDB_MovieID = candidate.ProviderItemID;
                break;
            case ("tvdb", "show"):
                series.TvdbShowExternalID = candidate.ProviderItemID;
                break;
            case ("tvdb", "movie"):
                series.TvdbMovieExternalID = candidate.ProviderItemID;
                break;
            default:
                logger.LogWarning("Unknown provider/type on candidate {CandidateID}: {Provider}/{Type}",
                    candidateID, candidate.Provider, candidate.ProviderType);
                return false;
        }

        // Series save must happen before candidate saves (cache refresh needed on MediaSeries).
        // Candidate updates (approved + siblings) are batched into a single transaction.
        RepoFactory.MediaSeries.Save(series);

        var now = DateTime.UtcNow;
        candidate.Status = "Approved";
        candidate.ReviewedAt = now;
        candidate.UpdatedAt = now;

        var siblings = RepoFactory.ProviderMatchCandidate.GetByMediaSeriesID(candidate.MediaSeriesID)
            .Where(c => c.ProviderMatchCandidateID != candidateID
                        && c.Provider == candidate.Provider
                        && c.ProviderType == candidate.ProviderType
                        && c.Status == "Pending")
            .ToList();
        foreach (var sibling in siblings)
        {
            sibling.Status = "Rejected";
            sibling.ReviewedAt = now;
            sibling.UpdatedAt = now;
        }

        var allUpdates = new List<ProviderMatchCandidate>(siblings.Count + 1) { candidate };
        allUpdates.AddRange(siblings);
        RepoFactory.ProviderMatchCandidate.Save(allUpdates);

        logger.LogInformation("Approved candidate {CandidateID}: {Provider}/{Type} ID {ProviderItemID} for series {MediaSeriesID}.",
            candidateID, candidate.Provider, candidate.ProviderType, candidate.ProviderItemID, candidate.MediaSeriesID);
        return true;
    }

    public bool RejectCandidate(int candidateID)
    {
        var candidate = RepoFactory.ProviderMatchCandidate.GetByID(candidateID);
        if (candidate is null || candidate.Status != "Pending")
            return false;

        candidate.Status = "Rejected";
        candidate.ReviewedAt = DateTime.UtcNow;
        candidate.UpdatedAt = DateTime.UtcNow;
        RepoFactory.ProviderMatchCandidate.Save(candidate);
        return true;
    }

    // ─────────────────────────── Queries ─────────────────────────────

    public IReadOnlyList<ProviderMatchCandidate> GetPendingCandidates()
        => RepoFactory.ProviderMatchCandidate.GetByStatus("Pending");

    public IReadOnlyList<ProviderMatchCandidate> GetCandidatesForSeries(int mediaSeriesID)
        => RepoFactory.ProviderMatchCandidate.GetByMediaSeriesID(mediaSeriesID);

    // ─────────────────────────── Helpers ─────────────────────────────

    /// <summary>
    /// Returns the existing candidate updated back to Pending, or a fresh one when no prior record exists.
    /// </summary>
    private static ProviderMatchCandidate UpsertCandidate(
        ProviderMatchCandidate? existing,
        int mediaSeriesID, string provider, int providerItemID, string providerType,
        string title, int? year, double score, List<string> reasons)
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
            MediaSeriesID = mediaSeriesID,
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

    /// <summary>
    /// Quick word-overlap check to skip candidates with no title similarity before running full scoring.
    /// </summary>
    private static bool PassesTitleFilter(HashSet<string> queryWords, string queryNormalized, string? candidateTitle)
    {
        if (string.IsNullOrWhiteSpace(candidateTitle))
            return false;
        var normalized = NormalizeTitle(candidateTitle);
        if (normalized.Length == 0)
            return false;
        if (normalized.Contains(queryNormalized, StringComparison.Ordinal) || queryNormalized.Contains(normalized, StringComparison.Ordinal))
            return true;
        foreach (var word in normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            if (queryWords.Contains(word))
                return true;
        return false;
    }

    // ─────────────────────────── Scoring ─────────────────────────────

    private static double ComputeScore(string queryTitle, int? queryYear, string candidateTitle, int? candidateYear, List<string> reasons)
    {
        if (string.IsNullOrWhiteSpace(candidateTitle))
            return 0;

        var query = NormalizeTitle(queryTitle);
        var candidate = NormalizeTitle(candidateTitle);

        if (query.Length == 0 || candidate.Length == 0)
            return 0;

        double titleScore;
        if (query == candidate)
        {
            titleScore = 1.0;
            reasons.Add("Exact title match");
        }
        else if (candidate.Contains(query, StringComparison.Ordinal) || query.Contains(candidate, StringComparison.Ordinal))
        {
            titleScore = 0.85;
            reasons.Add("Title substring match");
        }
        else
        {
            var queryWords = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var candidateWordSet = candidate.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .ToHashSet(StringComparer.Ordinal);
            var matchedWords = queryWords.Count(w => candidateWordSet.Contains(w));
            titleScore = queryWords.Length > 0 ? (double)matchedWords / queryWords.Length : 0;
            if (matchedWords > 0)
                reasons.Add($"{matchedWords}/{queryWords.Length} title words matched");
        }

        if (titleScore < MinScore)
            return titleScore;

        var yearBonus = 0.0;
        if (queryYear.HasValue && candidateYear.HasValue)
        {
            if (queryYear == candidateYear)
            {
                yearBonus = 0.1;
                reasons.Add("Year matches");
            }
            else if (Math.Abs(queryYear.Value - candidateYear.Value) <= 1)
            {
                yearBonus = 0.05;
                reasons.Add("Year within 1");
            }
        }

        return Math.Min(1.0, titleScore + yearBonus);
    }

    private static string NormalizeTitle(string title)
        => string.Join(' ', Regex.Replace(title.ToLowerInvariant().Trim(), @"[^a-z0-9 ]", " ")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
}
