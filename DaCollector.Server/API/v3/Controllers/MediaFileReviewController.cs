using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using DaCollector.Server.API.Annotations;
using DaCollector.Server.API.v3.Models.Common;
using DaCollector.Server.Media;
using DaCollector.Server.Models.Internal;
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
    MediaFileMatchCandidateService candidateService
) : BaseController(settingsProvider)
{
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
    public ActionResult<MediaFileReviewItem> SetManualMatch(
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

        return reviewService.SetManualMatch(fileID, request) is { } item ? Ok(item) : NotFound();
    }

    /// <summary>
    /// Approves a provider candidate and stores it as the file's locked manual match.
    /// </summary>
    [HttpPost("Candidates/{candidateID}/Approve")]
    [Authorize("admin")]
    public ActionResult<MediaFileReviewItem> ApproveCandidate([FromRoute] int candidateID)
        => candidateService.ApproveCandidate(candidateID) is { } item ? Ok(item) : NotFound();

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
}

public sealed record IgnoreFileRequest
{
    [MaxLength(1000)]
    public string? Reason { get; init; }
}
