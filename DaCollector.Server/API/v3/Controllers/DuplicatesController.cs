using System.ComponentModel.DataAnnotations;
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
    ExactDuplicateService exactDuplicateService
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
}
