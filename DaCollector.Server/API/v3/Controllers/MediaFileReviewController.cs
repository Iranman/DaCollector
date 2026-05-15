using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Quartz;
using DaCollector.Abstractions.User.Services;
using DaCollector.Abstractions.Video.Relocation;
using DaCollector.Abstractions.Video.Services;
using DaCollector.Server.API.Annotations;
using DaCollector.Server.API.v3.Models.Common;
using DaCollector.Server.Media;
using DaCollector.Server.Models.Internal;
using DaCollector.Server.Repositories;
using DaCollector.Server.Scheduling;
using DaCollector.Server.Scheduling.Jobs.TMDB;
using DaCollector.Server.Settings;

#nullable enable
namespace DaCollector.Server.API.v3.Controllers;

/// <summary>
/// Review queue for scanned local media files that are not linked to a known episode/movie entry.
/// </summary>
[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize]
public class MediaFileReviewController(
    ISettingsProvider settingsProvider,
    MediaFileReviewService reviewService,
    MediaFileMatchCandidateService candidateService,
    IUserDataService userDataService,
    ISchedulerFactory schedulerFactory,
    IVideoRelocationService relocationService
) : BaseController(settingsProvider)
{
    /// <summary>
    /// Returns local media files that are available on disk but appear corrupt:
    /// zero file size, or MediaInfo ran successfully but reported zero duration.
    /// Use <c>POST /api/v3/File/{fileID}/Rehash</c> to trigger a fresh hash + MediaInfo scan.
    /// </summary>
    [HttpGet("Files/Corrupt")]
    public ActionResult<ListResult<CorruptFileItem>> GetCorruptFiles(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100
    )
        => Ok(reviewService.GetCorruptFiles(page, pageSize));

    /// <summary>
    /// Returns local media files that are recorded in the database but no longer found on disk.
    /// Use <c>DELETE /api/v3/File/{fileID}?removeFiles=false</c> to remove a record without touching the disk.
    /// </summary>
    [HttpGet("Files/Missing")]
    public ActionResult<ListResult<MissingFileItem>> GetMissingFiles(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100
    )
        => Ok(reviewService.GetMissingFiles(page, pageSize));

    /// <summary>
    /// Returns unlinked local media files with persisted parser guesses and review state.
    /// </summary>
    [HttpGet("Files/Unmatched")]
    public ActionResult<ListResult<MediaFileReviewItem>> GetUnmatchedFiles(
        [FromQuery] bool includeIgnored = false,
        [FromQuery] bool includeBrokenCrossReferences = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100
    )
        => Ok(reviewService.GetUnmatchedFiles(includeIgnored, includeBrokenCrossReferences, page, pageSize));

    /// <summary>
    /// Returns parser and review state for one local media file.
    /// </summary>
    [HttpGet("Files/{fileID}")]
    public ActionResult<MediaFileReviewItem> GetFileReview([FromRoute] int fileID)
        => reviewService.GetFileReview(fileID) is { } item ? Ok(item) : NotFound();

    /// <summary>
    /// Re-runs filename parsing for one local media file and stores the latest parser result.
    /// </summary>
    [HttpPost("Files/{fileID}/RefreshParse")]
    [Authorize("admin")]
    public ActionResult<MediaFileReviewItem> RefreshParse([FromRoute] int fileID)
        => reviewService.GetFileReview(fileID, refreshParse: true) is { } item ? Ok(item) : NotFound();

    /// <summary>
    /// Scans one unmatched local media file against cached TMDB/TVDB records and stores reviewable candidates.
    /// </summary>
    [HttpPost("Files/{fileID}/ScanMatches")]
    [Authorize("admin")]
    public async Task<ActionResult<MediaFileMatchScanResult>> ScanFileMatches(
        [FromRoute] int fileID,
        [FromQuery] bool refreshParse = false,
        [FromQuery] bool includeOnlineSearch = false,
        [FromQuery] bool refreshExplicitIds = false
    )
    {
        var result = await candidateService.ScanFileAsync(fileID, refreshParse, includeOnlineSearch, refreshExplicitIds).ConfigureAwait(false);
        return result.Scanned ? Ok(result) : NotFound(result);
    }

    /// <summary>
    /// Scans all unmatched local media files against cached TMDB/TVDB records and stores reviewable candidates.
    /// </summary>
    [HttpPost("Files/ScanMatches")]
    [Authorize("admin")]
    public async Task<ActionResult<MediaFileMatchBatchScanResult>> ScanUnmatchedFileMatches(
        [FromQuery] bool includeIgnored = false,
        [FromQuery] bool refreshParse = false,
        [FromQuery] bool includeOnlineSearch = false,
        [FromQuery] bool refreshExplicitIds = false
    )
        => Ok(await candidateService.ScanUnmatchedFilesAsync(includeIgnored, refreshParse, includeOnlineSearch, refreshExplicitIds).ConfigureAwait(false));

    /// <summary>
    /// Returns all pending provider candidates for unmatched local media files.
    /// </summary>
    [HttpGet("Candidates")]
    public ActionResult<IReadOnlyList<MediaFileMatchCandidate>> GetPendingCandidates()
        => Ok(candidateService.GetPendingCandidates());

    /// <summary>
    /// Returns provider candidates for one unmatched local media file.
    /// </summary>
    [HttpGet("Files/{fileID}/Candidates")]
    public ActionResult<IReadOnlyList<MediaFileMatchCandidate>> GetFileCandidates([FromRoute] int fileID)
    {
        if (reviewService.GetFileReview(fileID) is null)
            return NotFound();
        return Ok(candidateService.GetCandidatesForFile(fileID));
    }

    /// <summary>
    /// Marks one local media file as ignored so scanner/review workflows stop treating it as unmatched media.
    /// </summary>
    [HttpPost("Files/{fileID}/Ignore")]
    [Authorize("admin")]
    public ActionResult<MediaFileReviewItem> IgnoreFile(
        [FromRoute] int fileID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] IgnoreFileRequest? request
    )
        => reviewService.IgnoreFile(fileID, request?.Reason) is { } item ? Ok(item) : NotFound();

    /// <summary>
    /// Removes the ignored flag from one local media file.
    /// </summary>
    [HttpPost("Files/{fileID}/Unignore")]
    [Authorize("admin")]
    public ActionResult<MediaFileReviewItem> UnignoreFile([FromRoute] int fileID)
        => reviewService.UnignoreFile(fileID) is { } item ? Ok(item) : NotFound();

    /// <summary>
    /// Stores a user-approved manual provider match for one local media file.
    /// </summary>
    [HttpPost("Files/{fileID}/ManualMatch")]
    [Authorize("admin")]
    public async Task<ActionResult<MediaFileReviewItem>> SetManualMatch(
        [FromRoute] int fileID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] ManualFileMatchRequest request
    )
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        if (string.IsNullOrWhiteSpace(request.EntityType))
            return ValidationProblem("EntityType is required.", nameof(request.EntityType));

        if (string.IsNullOrWhiteSpace(request.Provider))
            return ValidationProblem("Provider is required.", nameof(request.Provider));

        if (string.IsNullOrWhiteSpace(request.ProviderID))
            return ValidationProblem("ProviderID is required.", nameof(request.ProviderID));

        if (reviewService.SetManualMatch(fileID, request) is not { } item)
            return NotFound();

        var scheduler = await schedulerFactory.GetScheduler().ConfigureAwait(false);
        await scheduler.StartJob<ProcessFileTmdbJob>(c => c.VideoLocalID = fileID).ConfigureAwait(false);
        return Ok(item);
    }

    /// <summary>
    /// Approves a provider candidate and stores it as the file's locked manual match.
    /// </summary>
    [HttpPost("Candidates/{candidateID}/Approve")]
    [Authorize("admin")]
    public async Task<ActionResult<MediaFileReviewItem>> ApproveCandidate([FromRoute] int candidateID)
    {
        if (candidateService.ApproveCandidate(candidateID) is not { } item)
            return NotFound();

        var scheduler = await schedulerFactory.GetScheduler().ConfigureAwait(false);
        await scheduler.StartJob<ProcessFileTmdbJob>(c => c.VideoLocalID = item.FileID).ConfigureAwait(false);
        return Ok(item);
    }

    /// <summary>
    /// Rejects a provider candidate so it does not reappear during normal rescans.
    /// </summary>
    [HttpDelete("Candidates/{candidateID}")]
    [Authorize("admin")]
    public ActionResult RejectCandidate([FromRoute] int candidateID)
        => candidateService.RejectCandidate(candidateID) ? Ok() : NotFound();

    /// <summary>
    /// Clears a previously stored manual provider match.
    /// </summary>
    [HttpDelete("Files/{fileID}/ManualMatch")]
    [Authorize("admin")]
    public ActionResult<MediaFileReviewItem> ClearManualMatch([FromRoute] int fileID)
        => reviewService.ClearManualMatch(fileID) is { } item ? Ok(item) : NotFound();

    /// <summary>
    /// Returns the watched state for one local media file for the current user.
    /// Returns a default (unwatched) state if the file exists but has never been played.
    /// </summary>
    [HttpGet("Files/{fileID}/WatchedState")]
    public ActionResult<WatchedStateDto> GetWatchedState([FromRoute] int fileID)
    {
        var file = RepoFactory.VideoLocal.GetByID(fileID);
        if (file is null) return NotFound();
        var user = HttpContext.GetUser();
        var data = userDataService.GetVideoUserData(file, user);
        return Ok(data is null
            ? new WatchedStateDto()
            : new WatchedStateDto
            {
                IsWatched        = data.LastPlayedAt.HasValue,
                WatchedDate      = data.LastPlayedAt,
                WatchedCount     = data.PlaybackCount,
                ResumePositionMs = (long)data.ProgressPosition.TotalMilliseconds,
                LastUpdated      = data.LastUpdatedAt,
            });
    }

    /// <summary>
    /// Returns matched local media files where the configured renamer/mover would produce a different
    /// name or destination than the file's current path. No files are moved or renamed.
    /// To execute, call <c>POST /api/v3/File/{fileID}/Action/AutoRelocate</c> per file.
    /// </summary>
    [HttpGet("Files/RenamePlan")]
    [Authorize("admin")]
    public async Task<ActionResult<ListResult<RenamePlanItem>>> GetRenamePlan(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100
    )
    {
        if (relocationService.GetDefaultPipe() is not { ProviderInfo: { } } defaultPipe)
            return Ok(new ListResult<RenamePlanItem>(0, []));

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 500);

        var candidates = RepoFactory.VideoLocalPlace
            .GetAll()
            .Where(p => p.IsAvailable)
            .OrderBy(p => p.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var results = new List<RenamePlanItem>();
        foreach (var place in candidates)
        {
            var response = await relocationService.AutoRelocateFile(place, new AutoRelocateRequest
            {
                Preview = true,
                Rename = relocationService.RenameOnImport,
                Move = relocationService.MoveOnImport,
                Pipe = defaultPipe,
                AllowRelocationInsideDestination = relocationService.AllowRelocationInsideDestinationOnImport,
            }).ConfigureAwait(false);

            if (!response.Success || (!response.Renamed && !response.Moved))
                continue;

            results.Add(new RenamePlanItem
            {
                FileID = place.VideoID,
                LocationID = place.ID,
                ManagedFolderID = place.ManagedFolderID,
                CurrentRelativePath = place.RelativePath,
                ProposedRelativePath = response.RelativePath,
                WouldRename = response.Renamed,
                WouldMove = response.Moved,
            });
        }

        var total = results.Count;
        return Ok(new ListResult<RenamePlanItem>(
            total,
            results.Skip((page - 1) * pageSize).Take(pageSize).ToList()
        ));
    }

    /// <summary>
    /// Sets the watched state for one local media file for the current user.
    /// </summary>
    [HttpPost("Files/{fileID}/WatchedState")]
    [Authorize("admin")]
    public async Task<ActionResult<WatchedStateDto>> SetWatchedState(
        [FromRoute] int fileID,
        [FromBody] SetWatchedRequest request
    )
    {
        var file = RepoFactory.VideoLocal.GetByID(fileID);
        if (file is null) return NotFound();
        var user = HttpContext.GetUser();
        var isWatched = request.IsWatched ?? false;
        var lastPlayedAt = isWatched ? (request.WatchedDate ?? DateTime.UtcNow) : (DateTime?)null;
        var data = await userDataService.SetVideoWatchedStatus(file, user, isWatched, lastPlayedAt).ConfigureAwait(false);
        return Ok(new WatchedStateDto
        {
            IsWatched        = data.LastPlayedAt.HasValue,
            WatchedDate      = data.LastPlayedAt,
            WatchedCount     = data.PlaybackCount,
            ResumePositionMs = (long)data.ProgressPosition.TotalMilliseconds,
            LastUpdated      = data.LastUpdatedAt,
        });
    }
}

public sealed record RenamePlanItem
{
    public int FileID { get; init; }

    public int LocationID { get; init; }

    public int ManagedFolderID { get; init; }

    public string CurrentRelativePath { get; init; } = string.Empty;

    public string ProposedRelativePath { get; init; } = string.Empty;

    public bool WouldRename { get; init; }

    public bool WouldMove { get; init; }
}

public sealed record IgnoreFileRequest
{
    [MaxLength(1000)]
    public string? Reason { get; init; }
}

public sealed record WatchedStateDto
{
    public bool IsWatched { get; init; }
    public DateTime? WatchedDate { get; init; }
    public int WatchedCount { get; init; }
    public long ResumePositionMs { get; init; }
    public DateTime? LastUpdated { get; init; }
}

public sealed record SetWatchedRequest
{
    public bool? IsWatched { get; init; }
    public DateTime? WatchedDate { get; init; }
}
