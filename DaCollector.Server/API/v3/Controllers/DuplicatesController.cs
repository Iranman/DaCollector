using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DaCollector.Abstractions.Duplicates;
using DaCollector.Server.API.Annotations;
using DaCollector.Server.API.v3.Helpers;
using DaCollector.Server.API.v3.Models.Common;
using DaCollector.Server.Duplicates;
using DaCollector.Server.Settings;

#nullable enable
namespace DaCollector.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize]
public class DuplicatesController(
    ISettingsProvider settingsProvider,
    ExactDuplicateService exactDuplicateService,
    MediaDuplicateReviewService mediaDuplicateReviewService
) : BaseController(settingsProvider)
{
    /// <summary>
    /// Summarize exact duplicate file locations by hash and file size.
    /// </summary>
    [HttpGet("Exact/Summary")]
    public ActionResult<ExactDuplicateSummary> GetExactDuplicateSummary(
        [FromQuery] bool includeIgnored = false,
        [FromQuery] bool onlyAvailable = false,
        [FromQuery] int? preferredManagedFolderID = null,
        [FromQuery] string? preferredPathContains = null
    ) =>
        exactDuplicateService.GetSummary(includeIgnored, onlyAvailable, preferredManagedFolderID, preferredPathContains);

    /// <summary>
    /// List exact duplicate file locations by hash and file size.
    /// </summary>
    [HttpGet("Exact")]
    public ActionResult<ListResult<ExactDuplicateSet>> GetExactDuplicates(
        [FromQuery] bool includeIgnored = false,
        [FromQuery] bool onlyAvailable = false,
        [FromQuery] int? preferredManagedFolderID = null,
        [FromQuery] string? preferredPathContains = null,
        [FromQuery, Range(0, 1000)] int pageSize = 100,
        [FromQuery, Range(1, int.MaxValue)] int page = 1
    ) =>
        exactDuplicateService
            .GetExactDuplicates(includeIgnored, onlyAvailable, preferredManagedFolderID, preferredPathContains)
            .ToListResult(page, pageSize);

    /// <summary>
    /// Return non-destructive cleanup recommendations for exact duplicate sets.
    /// </summary>
    [HttpGet("Exact/CleanupPlan")]
    public ActionResult<ListResult<ExactDuplicateCleanupPlan>> GetExactDuplicateCleanupPlans(
        [FromQuery] bool includeIgnored = false,
        [FromQuery] bool onlyAvailable = false,
        [FromQuery] int? preferredManagedFolderID = null,
        [FromQuery] string? preferredPathContains = null,
        [FromQuery, Range(0, 1000)] int pageSize = 100,
        [FromQuery, Range(1, int.MaxValue)] int page = 1
    ) =>
        exactDuplicateService
            .GetCleanupPlans(includeIgnored, onlyAvailable, preferredManagedFolderID, preferredPathContains)
            .ToListResult(page, pageSize);

    /// <summary>
    /// List possible duplicate Plex media entries by provider ID, title/year, and path hash signals.
    /// </summary>
    [HttpGet("Media/Plex/Library/{sectionKey}")]
    public async Task<ActionResult<ListResult<MediaDuplicateSet>>> GetPlexMediaDuplicates(
        [FromRoute] string sectionKey,
        [FromQuery, Range(0, 1000)] int pageSize = 100,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            return (await mediaDuplicateReviewService
                    .GetPlexMediaDuplicates(sectionKey, cancellationToken: cancellationToken)
                    .ConfigureAwait(false))
                .ToListResult(page, pageSize);
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException)
        {
            return ValidationProblem(e.Message, "Plex");
        }
    }

    /// <summary>
    /// Preview or delete one exact duplicate remove candidate.
    /// </summary>
    [Authorize("admin")]
    [HttpDelete("Exact/Location/{locationID:int}")]
    public async Task<ActionResult<ExactDuplicateDeleteResult>> DeleteExactDuplicateLocation(
        [FromRoute, Range(1, int.MaxValue)] int locationID,
        [FromQuery] bool confirm = false,
        [FromQuery] bool deleteFile = true,
        [FromQuery] bool deleteEmptyFolders = true,
        [FromQuery] bool includeIgnored = false,
        [FromQuery] bool onlyAvailable = false,
        [FromQuery] int? preferredManagedFolderID = null,
        [FromQuery] string? preferredPathContains = null
    )
    {
        var result = await exactDuplicateService.DeleteRemoveCandidate(
            locationID,
            confirm,
            deleteFile,
            deleteEmptyFolders,
            includeIgnored,
            onlyAvailable,
            preferredManagedFolderID,
            preferredPathContains
        ).ConfigureAwait(false);

        return result is null
            ? ValidationProblem($"Location {locationID} is not a current exact duplicate remove candidate.")
            : result;
    }
}
