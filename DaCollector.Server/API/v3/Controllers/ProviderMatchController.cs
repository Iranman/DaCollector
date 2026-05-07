using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DaCollector.Server.API.Annotations;
using DaCollector.Server.Models.Internal;
using DaCollector.Server.Providers;
using DaCollector.Server.Repositories;
using DaCollector.Server.Settings;

#nullable enable
namespace DaCollector.Server.API.v3.Controllers;

/// <summary>
/// Review queue for provider match candidates (TMDB / TVDB) linked to a MediaSeries.
/// </summary>
[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize]
public class ProviderMatchController(ISettingsProvider settingsProvider, ProviderMatchQueueService matchQueueService) : BaseController(settingsProvider)
{
    /// <summary>
    /// Returns all pending provider match candidates across all series.
    /// </summary>
    [HttpGet("Candidates")]
    public ActionResult<IReadOnlyList<ProviderMatchCandidate>> GetPendingCandidates()
        => Ok(matchQueueService.GetPendingCandidates());

    /// <summary>
    /// Returns all provider match candidates for one series.
    /// </summary>
    [HttpGet("Candidates/Series/{mediaSeriesID}")]
    public ActionResult<IReadOnlyList<ProviderMatchCandidate>> GetCandidatesForSeries([FromRoute] int mediaSeriesID)
    {
        if (RepoFactory.MediaSeries.GetByID(mediaSeriesID) is null)
            return NotFound();
        return Ok(matchQueueService.GetCandidatesForSeries(mediaSeriesID));
    }

    /// <summary>
    /// Scans locally cached provider data to build match candidates for one series.
    /// </summary>
    [HttpPost("Series/{mediaSeriesID}/Scan")]
    [Authorize("admin")]
    public async Task<ActionResult> ScanSeries([FromRoute] int mediaSeriesID)
    {
        if (RepoFactory.MediaSeries.GetByID(mediaSeriesID) is null)
            return NotFound();
        await matchQueueService.ScanSeriesAsync(mediaSeriesID);
        return Ok();
    }

    /// <summary>
    /// Approves a candidate, applying its provider ID to the linked MediaSeries.
    /// Also rejects remaining pending candidates for the same series/provider/type.
    /// </summary>
    [HttpPost("Candidates/{candidateID}/Approve")]
    [Authorize("admin")]
    public ActionResult ApproveCandidate([FromRoute] int candidateID)
    {
        var candidate = RepoFactory.ProviderMatchCandidate.GetByID(candidateID);
        if (candidate is null)
            return NotFound();
        if (candidate.Status != "Pending")
            return BadRequest($"Candidate {candidateID} is already {candidate.Status}.");
        if (!matchQueueService.ApproveCandidate(candidateID))
            return BadRequest("Approval failed. The series or provider type may be unsupported.");
        return Ok();
    }

    /// <summary>
    /// Rejects a candidate so it does not reappear in the pending queue.
    /// </summary>
    [HttpDelete("Candidates/{candidateID}")]
    [Authorize("admin")]
    public ActionResult RejectCandidate([FromRoute] int candidateID)
    {
        var candidate = RepoFactory.ProviderMatchCandidate.GetByID(candidateID);
        if (candidate is null)
            return NotFound();
        if (candidate.Status != "Pending")
            return BadRequest($"Candidate {candidateID} is already {candidate.Status}.");
        if (!matchQueueService.RejectCandidate(candidateID))
            return BadRequest("Rejection failed.");
        return Ok();
    }
}
