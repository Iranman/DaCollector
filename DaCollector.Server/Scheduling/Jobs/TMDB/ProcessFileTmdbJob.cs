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
using DaCollector.Server.Providers.TMDB;
using DaCollector.Server.Repositories;
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

        // Skip if already cross-referenced
        if (RepoFactory.CrossRef_File_TmdbMovie.GetByVideoLocalID(VideoLocalID).Count > 0 ||
            RepoFactory.CrossRef_File_TmdbEpisode.GetByVideoLocalID(VideoLocalID).Count > 0)
        {
            _logger.LogDebug("File {ID} already has TMDB cross-references; skipping.", VideoLocalID);
            return;
        }

        var now = DateTime.UtcNow;

        // Use scored candidates produced by MediaFileMatchCandidateService.ScanFileAsync
        var candidates = _candidateService.GetCandidatesForFile(VideoLocalID);

        var approvedTmdb = candidates.FirstOrDefault(c => c.Status == "Approved" && c.Provider == "tmdb");
        if (approvedTmdb != null)
        {
            WriteTmdbCrossRef(approvedTmdb, now);
            return;
        }

        var bestPending = candidates
            .Where(c => c.Status == "Pending" && c.Provider == "tmdb")
            .MaxBy(c => c.ConfidenceScore);

        if (bestPending != null)
        {
            if (bestPending.ConfidenceScore >= MediaFileMatchCandidateService.AutoMatchThreshold)
            {
                _logger.LogInformation("Using high-confidence TMDB candidate for file {ID}: {Title} (score {Score:F4})",
                    VideoLocalID, bestPending.Title, bestPending.ConfidenceScore);
                WriteTmdbCrossRef(bestPending, now);
            }
            else
            {
                _logger.LogInformation("File {ID} has TMDB candidates below threshold (best: {Score:F4}); leaving in review queue.",
                    VideoLocalID, bestPending.ConfidenceScore);
            }
            return;
        }

        // No scored candidates — fall back to raw TMDB filename search
        var rawName = Path.GetFileNameWithoutExtension(_fileName ?? _vlocal.FirstValidPlace?.RelativePath ?? string.Empty);
        if (string.IsNullOrWhiteSpace(rawName))
        {
            _logger.LogWarning("Could not extract filename for VideoLocal {ID}; skipping TMDB match", VideoLocalID);
            return;
        }

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
            return;
        }

        var (showResults, _) = await _tmdbSearchService.SearchShowsRaw(rawName).ConfigureAwait(false);
        if (showResults.Count > 0)
        {
            var best = showResults[0];
            _logger.LogInformation("TMDB show match for '{Name}': {Title} (ID {ID})", rawName, best.OriginalName, best.Id);

            var episodes = RepoFactory.TMDB_Episode.GetByTmdbShowID(best.Id);
            if (episodes.Count > 0)
            {
                var firstEp = episodes[0];
                var xref = new CrossRef_File_TmdbEpisode
                {
                    VideoLocalID = VideoLocalID,
                    TmdbEpisodeID = firstEp.TmdbEpisodeID,
                    Percentage = 100,
                    EpisodeOrder = 1,
                    IsManuallyLinked = false,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                RepoFactory.CrossRef_File_TmdbEpisode.Save(xref);
            }
            else
            {
                _logger.LogDebug("No cached TMDB episodes for show ID {ShowID}; cross-reference not created", best.Id);
            }

            _seriesService.GetOrCreateSeriesFromProvider("tmdb", best.Id, "show");
            return;
        }

        _logger.LogInformation("No TMDB match found for '{Name}' (VideoLocal {ID})", rawName, VideoLocalID);
    }

    private void WriteTmdbCrossRef(MediaFileMatchCandidate candidate, DateTime now)
    {
        if (candidate.ProviderType == "movie")
        {
            var xref = new CrossRef_File_TmdbMovie
            {
                VideoLocalID = VideoLocalID,
                TmdbMovieID = candidate.ProviderItemID,
                IsManuallyLinked = false,
                CreatedAt = now,
                UpdatedAt = now
            };
            RepoFactory.CrossRef_File_TmdbMovie.Save(xref);
            _logger.LogInformation("Linked file {ID} to TMDB movie {MovieID} ({Title})", VideoLocalID, candidate.ProviderItemID, candidate.Title);
        }
        else if (candidate.ProviderType == "show")
        {
            var episodes = RepoFactory.TMDB_Episode.GetByTmdbShowID(candidate.ProviderItemID);
            if (episodes.Count > 0)
            {
                var firstEp = episodes[0];
                var xref = new CrossRef_File_TmdbEpisode
                {
                    VideoLocalID = VideoLocalID,
                    TmdbEpisodeID = firstEp.TmdbEpisodeID,
                    Percentage = 100,
                    EpisodeOrder = 1,
                    IsManuallyLinked = false,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                RepoFactory.CrossRef_File_TmdbEpisode.Save(xref);
                _logger.LogInformation("Linked file {ID} to TMDB show {ShowID} ({Title}), episode {EpID}",
                    VideoLocalID, candidate.ProviderItemID, candidate.Title, firstEp.TmdbEpisodeID);
            }
            else
            {
                _logger.LogDebug("No cached TMDB episodes for show ID {ShowID}; episode cross-reference not created", candidate.ProviderItemID);
            }
        }
        _seriesService.GetOrCreateSeriesFromProvider("tmdb", candidate.ProviderItemID, candidate.ProviderType);
    }

    public ProcessFileTmdbJob(TmdbSearchService tmdbSearchService, MediaSeriesService seriesService, MediaFileMatchCandidateService candidateService)
    {
        _tmdbSearchService = tmdbSearchService;
        _seriesService = seriesService;
        _candidateService = candidateService;
    }

    protected ProcessFileTmdbJob() { }
}
