using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartz;
using DaCollector.Server.Media;
using DaCollector.Server.Models.CrossReference;
using DaCollector.Server.Models.DaCollector;
using DaCollector.Server.Models.Internal;
using DaCollector.Server.Models.TMDB;
using DaCollector.Server.Models.TVDB;
using DaCollector.Server.Providers.TMDB;
using DaCollector.Server.Repositories;
using DaCollector.Server.Repositories.Direct;
using DaCollector.Server.Scheduling;
using DaCollector.Server.Scheduling.Acquisition.Attributes;
using DaCollector.Server.Scheduling.Attributes;
using DaCollector.Server.Scheduling.Concurrency;
using DaCollector.Server.Services;
using DaCollector.Server.Utilities;

#pragma warning disable CS8618
#nullable enable
namespace DaCollector.Server.Scheduling.Jobs.TMDB;

[DatabaseRequired]
[NetworkRequired]
[DisallowConcurrentExecution]
[LimitConcurrency(4, 8)]
[JobKeyGroup(JobKeyGroup.TMDB)]
public class ProcessFileTmdbJob : BaseJob
{
    private readonly TmdbSearchService _tmdbSearchService;
    private readonly MediaSeriesService _seriesService;
    private readonly MediaFileMatchCandidateService _candidateService;
    private readonly MediaFileReviewStateRepository _reviewStateRepository;
    private readonly ISchedulerFactory _schedulerFactory;

    private VideoLocal _vlocal;
    private string _fileName;

    public int VideoLocalID { get; set; }

    public override string TypeName => "Match Video to TMDB";
    public override string Title => "Matching Video to TMDB";
    public override Dictionary<string, object> Details
    {
        get
        {
            var result = new Dictionary<string, object>();
            if (string.IsNullOrEmpty(_fileName))
                result["Video"] = VideoLocalID;
            else
                result["File Path"] = _fileName;
            return result;
        }
    }

    public override void PostInit()
    {
        _vlocal = RepoFactory.VideoLocal.GetByID(VideoLocalID);
        if (_vlocal == null) throw new JobExecutionException($"VideoLocal not found: {VideoLocalID}");
        _fileName = Utils.GetDistinctPath(_vlocal.FirstValidPlace?.Path);
    }

    public override async Task Process()
    {
        _logger.LogInformation("Processing {Job}: {FileName}", nameof(ProcessFileTmdbJob), _fileName ?? VideoLocalID.ToString());

        if (_vlocal == null)
        {
            _vlocal = RepoFactory.VideoLocal.GetByID(VideoLocalID);
            if (_vlocal == null)
                return;
        }

        // Skip if file is already cross-referenced to any provider.
        if (RepoFactory.CrossRef_File_TmdbMovie.GetByVideoLocalID(VideoLocalID).Count > 0 ||
            RepoFactory.CrossRef_File_TmdbEpisode.GetByVideoLocalID(VideoLocalID).Count > 0 ||
            RepoFactory.CrossRef_File_TvdbEpisode.GetByVideoLocalID(VideoLocalID).Count > 0)
        {
            _logger.LogDebug("File {ID} already has cross-references; skipping.", VideoLocalID);
            return;
        }

        var now = DateTime.UtcNow;
        var state = _reviewStateRepository.GetByVideoLocalID(VideoLocalID);
        var candidates = _candidateService.GetCandidatesForFile(VideoLocalID);

        // Use any approved candidate (TMDB or TVDB).
        var approvedCandidate = candidates.FirstOrDefault(c => c.Status == "Approved");
        if (approvedCandidate != null)
        {
            WriteCrossRefByProvider(approvedCandidate.Provider, approvedCandidate.ProviderItemID, approvedCandidate.ProviderType, state, now);
            return;
        }

        // Use a directly-set manual match (SetManualMatch called without going through a candidate).
        if (state is { ManualProvider: { } manualProvider, ManualProviderID: { } manualProviderID }
            && int.TryParse(manualProviderID, out var manualId))
        {
            var providerType = state.ManualEntityType?.ToLowerInvariant() == "show" ? "show" : "movie";
            WriteCrossRefByProvider(manualProvider, manualId, providerType, state, now);
            return;
        }

        // Use high-confidence pending TMDB candidate.
        var bestPending = candidates
            .Where(c => c.Status == "Pending" && c.Provider == "tmdb")
            .MaxBy(c => c.ConfidenceScore);

        if (bestPending != null)
        {
            if (bestPending.ConfidenceScore >= MediaFileMatchCandidateService.AutoMatchThreshold)
            {
                _logger.LogInformation("Using high-confidence TMDB candidate for file {ID}: {Title} (score {Score:F4})",
                    VideoLocalID, bestPending.Title, bestPending.ConfidenceScore);
                WriteCrossRefByProvider(bestPending.Provider, bestPending.ProviderItemID, bestPending.ProviderType, state, now);
            }
            else
            {
                _logger.LogInformation("File {ID} has TMDB candidates below threshold (best: {Score:F4}); leaving in review queue.",
                    VideoLocalID, bestPending.ConfidenceScore);
            }
            return;
        }

        // No scored candidates — fall back to raw TMDB filename search.
        var rawName = Path.GetFileNameWithoutExtension(_fileName ?? _vlocal.FirstValidPlace?.RelativePath ?? string.Empty);
        if (string.IsNullOrWhiteSpace(rawName))
        {
            _logger.LogWarning("Could not extract filename for VideoLocal {ID}; skipping TMDB match", VideoLocalID);
            return;
        }

        var scheduler = await _schedulerFactory.GetScheduler().ConfigureAwait(false);

        var (movieResults, _) = await _tmdbSearchService.SearchMoviesRaw(rawName).ConfigureAwait(false);
        if (movieResults.Count > 0)
        {
            var best = movieResults[0];
            _logger.LogInformation("TMDB movie match for '{Name}': {Title} (ID {ID})", rawName, best.OriginalTitle, best.Id);

            var xref = new CrossRef_File_TmdbMovie
            {
                VideoLocalID = VideoLocalID,
                TmdbMovieID = best.Id,
                IsManuallyLinked = false,
                CreatedAt = now,
                UpdatedAt = now
            };
            RepoFactory.CrossRef_File_TmdbMovie.Save(xref);
            _seriesService.GetOrCreateSeriesFromProvider("tmdb", best.Id, "movie");
            await scheduler.StartJob<UpdateTmdbMovieJob>(c => c.TmdbMovieID = best.Id).ConfigureAwait(false);
            return;
        }

        var (showResults, _) = await _tmdbSearchService.SearchShowsRaw(rawName).ConfigureAwait(false);
        if (showResults.Count > 0)
        {
            var best = showResults[0];
            _logger.LogInformation("TMDB show match for '{Name}': {Title} (ID {ID})", rawName, best.OriginalName, best.Id);

            var episodes = ResolveEpisodes(best.Id, state);
            if (episodes.Count > 0)
                WriteTmdbEpisodeXrefs(episodes, now);
            else
                _logger.LogDebug("No cached TMDB episodes for show ID {ShowID}; episode cross-reference will be created after metadata refresh", best.Id);

            _seriesService.GetOrCreateSeriesFromProvider("tmdb", best.Id, "show");
            await scheduler.StartJob<UpdateTmdbShowJob>(c => c.TmdbShowID = best.Id).ConfigureAwait(false);
            return;
        }

        _logger.LogInformation("No TMDB match found for '{Name}' (VideoLocal {ID})", rawName, VideoLocalID);
    }

    private void WriteCrossRefByProvider(string provider, int providerItemID, string providerType, MediaFileReviewState? state, DateTime now)
    {
        switch (provider, providerType)
        {
            case ("tmdb", "movie"):
                RepoFactory.CrossRef_File_TmdbMovie.Save(new CrossRef_File_TmdbMovie
                {
                    VideoLocalID = VideoLocalID,
                    TmdbMovieID = providerItemID,
                    IsManuallyLinked = false,
                    CreatedAt = now,
                    UpdatedAt = now,
                });
                _logger.LogInformation("Linked file {ID} to TMDB movie {MovieID}", VideoLocalID, providerItemID);
                break;

            case ("tmdb", "show"):
                var tmdbEpisodes = ResolveEpisodes(providerItemID, state);
                if (tmdbEpisodes.Count > 0)
                {
                    WriteTmdbEpisodeXrefs(tmdbEpisodes, now);
                    _logger.LogInformation("Linked file {ID} to TMDB show {ShowID}, {Count} episode(s)", VideoLocalID, providerItemID, tmdbEpisodes.Count);
                }
                else
                    _logger.LogDebug("No cached TMDB episodes for show ID {ShowID}; episode cross-reference not created", providerItemID);
                break;

            case ("tvdb", "show"):
                var tvdbEpisodes = ResolveTvdbEpisodes(providerItemID, state);
                if (tvdbEpisodes.Count > 0)
                {
                    WriteTvdbEpisodeXrefs(tvdbEpisodes, now);
                    _logger.LogInformation("Linked file {ID} to TVDB show {ShowID}, {Count} episode(s)", VideoLocalID, providerItemID, tvdbEpisodes.Count);
                }
                else
                    _logger.LogDebug("No cached TVDB episodes for show ID {ShowID}; episode cross-reference not created", providerItemID);
                break;

            case ("tvdb", "movie"):
                _logger.LogWarning("TVDB movie cross-references are not yet supported; file {ID} will not be linked.", VideoLocalID);
                break;

            default:
                _logger.LogWarning("Unknown provider/type {Provider}/{Type} for file {ID}; skipping.", provider, providerType, VideoLocalID);
                break;
        }

        _seriesService.GetOrCreateSeriesFromProvider(provider, providerItemID, providerType);
    }

    private IReadOnlyList<TMDB_Episode> ResolveEpisodes(int tmdbShowId, MediaFileReviewState? state)
    {
        var allEpisodes = RepoFactory.TMDB_Episode.GetByTmdbShowID(tmdbShowId);
        if (allEpisodes.Count == 0)
            return [];

        var seasonNumber = state?.ParsedSeasonNumber ?? 1;
        var episodeNumbers = state?.ParsedEpisodeNumbers;

        if (episodeNumbers is { Count: > 0 })
        {
            var matched = allEpisodes
                .Where(e => e.SeasonNumber == seasonNumber && episodeNumbers.Contains(e.EpisodeNumber))
                .OrderBy(e => e.EpisodeNumber)
                .ToList();
            if (matched.Count > 0)
                return matched;

            _logger.LogDebug("No TMDB episodes matched S{Season}E{Episodes} for show {ShowID}; falling back to S01E01",
                seasonNumber, string.Join("+", episodeNumbers), tmdbShowId);
        }

        var fallback = allEpisodes.FirstOrDefault(e => e.SeasonNumber == 1 && e.EpisodeNumber == 1)
            ?? allEpisodes.OrderBy(e => e.SeasonNumber).ThenBy(e => e.EpisodeNumber).First();
        return [fallback];
    }

    private IReadOnlyList<TVDB_Episode> ResolveTvdbEpisodes(int tvdbShowId, MediaFileReviewState? state)
    {
        var allEpisodes = RepoFactory.TVDB_Episode.GetByTvdbShowID(tvdbShowId);
        if (allEpisodes.Count == 0)
            return [];

        var seasonNumber = state?.ParsedSeasonNumber ?? 1;
        var episodeNumbers = state?.ParsedEpisodeNumbers;

        if (episodeNumbers is { Count: > 0 })
        {
            var matched = allEpisodes
                .Where(e => e.SeasonNumber == seasonNumber && episodeNumbers.Contains(e.EpisodeNumber))
                .OrderBy(e => e.EpisodeNumber)
                .ToList();
            if (matched.Count > 0)
                return matched;

            _logger.LogDebug("No TVDB episodes matched S{Season}E{Episodes} for show {ShowID}; falling back to S01E01",
                seasonNumber, string.Join("+", episodeNumbers), tvdbShowId);
        }

        var fallback = allEpisodes.FirstOrDefault(e => e.SeasonNumber == 1 && e.EpisodeNumber == 1)
            ?? allEpisodes.OrderBy(e => e.SeasonNumber).ThenBy(e => e.EpisodeNumber).First();
        return [fallback];
    }

    private void WriteTmdbEpisodeXrefs(IReadOnlyList<TMDB_Episode> episodes, DateTime now)
    {
        var pctEach = 100 / episodes.Count;
        var remainder = 100 - pctEach * episodes.Count;
        for (var i = 0; i < episodes.Count; i++)
        {
            RepoFactory.CrossRef_File_TmdbEpisode.Save(new CrossRef_File_TmdbEpisode
            {
                VideoLocalID = VideoLocalID,
                TmdbEpisodeID = episodes[i].TmdbEpisodeID,
                Percentage = pctEach + (i == episodes.Count - 1 ? remainder : 0),
                EpisodeOrder = i + 1,
                IsManuallyLinked = false,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }
    }

    private void WriteTvdbEpisodeXrefs(IReadOnlyList<TVDB_Episode> episodes, DateTime now)
    {
        var pctEach = 100 / episodes.Count;
        var remainder = 100 - pctEach * episodes.Count;
        for (var i = 0; i < episodes.Count; i++)
        {
            RepoFactory.CrossRef_File_TvdbEpisode.Save(new CrossRef_File_TvdbEpisode
            {
                VideoLocalID = VideoLocalID,
                TvdbEpisodeID = episodes[i].TvdbEpisodeID,
                Percentage = pctEach + (i == episodes.Count - 1 ? remainder : 0),
                EpisodeOrder = i + 1,
                IsManuallyLinked = false,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }
    }

    public ProcessFileTmdbJob(TmdbSearchService tmdbSearchService, MediaSeriesService seriesService, MediaFileMatchCandidateService candidateService, MediaFileReviewStateRepository reviewStateRepository, ISchedulerFactory schedulerFactory)
    {
        _tmdbSearchService = tmdbSearchService;
        _seriesService = seriesService;
        _candidateService = candidateService;
        _reviewStateRepository = reviewStateRepository;
        _schedulerFactory = schedulerFactory;
    }

    protected ProcessFileTmdbJob() { }
}
