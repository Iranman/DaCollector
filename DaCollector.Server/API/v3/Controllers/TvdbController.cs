using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Quartz;
using DaCollector.Server.API.Annotations;
using DaCollector.Server.Models.DaCollector;
using DaCollector.Server.Models.TVDB;
using DaCollector.Server.Providers.TVDB;
using DaCollector.Server.Repositories;
using DaCollector.Server.Scheduling;
using DaCollector.Server.Scheduling.Jobs.TVDB;
using DaCollector.Server.Settings;

#nullable enable
namespace DaCollector.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize]
public class TvdbController : BaseController
{
    private readonly ILogger<TvdbController> _logger;
    private readonly ISchedulerFactory _schedulerFactory;

    public TvdbController(ISettingsProvider settingsProvider, ILogger<TvdbController> logger, ISchedulerFactory schedulerFactory) : base(settingsProvider)
    {
        _logger = logger;
        _schedulerFactory = schedulerFactory;
    }

    // ─────────────────────────── Show endpoints ───────────────────────────

    /// <summary>
    /// Returns a cached TVDB show by its external TVDB show ID.
    /// </summary>
    [HttpGet("Show/{tvdbShowID}")]
    public ActionResult<TVDB_Show> GetShow([FromRoute] int tvdbShowID)
    {
        var show = RepoFactory.TVDB_Show.GetByTvdbShowID(tvdbShowID);
        return show is null ? NotFound() : show;
    }

    /// <summary>
    /// Returns all seasons for a TVDB show.
    /// </summary>
    [HttpGet("Show/{tvdbShowID}/Seasons")]
    public ActionResult<IEnumerable<TVDB_Season>> GetShowSeasons([FromRoute] int tvdbShowID)
    {
        if (RepoFactory.TVDB_Show.GetByTvdbShowID(tvdbShowID) is null)
            return NotFound();
        return RepoFactory.TVDB_Season.GetByTvdbShowID(tvdbShowID).ToList();
    }

    /// <summary>
    /// Returns all episodes for a TVDB show.
    /// </summary>
    [HttpGet("Show/{tvdbShowID}/Episodes")]
    public ActionResult<IEnumerable<TVDB_Episode>> GetShowEpisodes([FromRoute] int tvdbShowID)
    {
        if (RepoFactory.TVDB_Show.GetByTvdbShowID(tvdbShowID) is null)
            return NotFound();
        return RepoFactory.TVDB_Episode.GetByTvdbShowID(tvdbShowID).ToList();
    }

    /// <summary>
    /// Queues a refresh of a TVDB show from the TVDB API.
    /// </summary>
    [HttpPost("Show/{tvdbShowID}/Refresh")]
    public async Task<ActionResult> RefreshShow([FromRoute] int tvdbShowID)
    {
        var scheduler = await _schedulerFactory.GetScheduler();
        await scheduler.StartJob<GetTvdbShowJob>(job => job.TvdbShowID = tvdbShowID);
        return Ok();
    }

    /// <summary>
    /// Links a TVDB show (by external TVDB ID) to a MediaSeries.
    /// </summary>
    [HttpPost("Show/{tvdbShowID}/Link/{seriesID}")]
    public ActionResult LinkShow([FromRoute] int tvdbShowID, [FromRoute] int seriesID)
    {
        if (RepoFactory.TVDB_Show.GetByTvdbShowID(tvdbShowID) is null)
            return NotFound($"TVDB show {tvdbShowID} not in cache. Refresh it first.");

        var series = RepoFactory.MediaSeries.GetByID(seriesID);
        if (series is null)
            return NotFound($"MediaSeries {seriesID} not found.");

        series.TvdbShowExternalID = tvdbShowID;
        series.TvdbMovieExternalID = null;
        RepoFactory.MediaSeries.Save(series, false, false);
        return Ok();
    }

    /// <summary>
    /// Removes the TVDB show link from a MediaSeries.
    /// </summary>
    [HttpDelete("Show/{tvdbShowID}/Link/{seriesID}")]
    public ActionResult UnlinkShow([FromRoute] int tvdbShowID, [FromRoute] int seriesID)
    {
        var series = RepoFactory.MediaSeries.GetByID(seriesID);
        if (series is null)
            return NotFound($"MediaSeries {seriesID} not found.");

        if (series.TvdbShowExternalID != tvdbShowID)
            return BadRequest($"MediaSeries {seriesID} is not linked to TVDB show {tvdbShowID}.");

        series.TvdbShowExternalID = null;
        RepoFactory.MediaSeries.Save(series, false, false);
        return Ok();
    }

    // ─────────────────────────── Movie endpoints ──────────────────────────

    /// <summary>
    /// Returns a cached TVDB movie by its external TVDB movie ID.
    /// </summary>
    [HttpGet("Movie/{tvdbMovieID}")]
    public ActionResult<TVDB_Movie> GetMovie([FromRoute] int tvdbMovieID)
    {
        var movie = RepoFactory.TVDB_Movie.GetByTvdbMovieID(tvdbMovieID);
        return movie is null ? NotFound() : movie;
    }

    /// <summary>
    /// Queues a refresh of a TVDB movie from the TVDB API.
    /// </summary>
    [HttpPost("Movie/{tvdbMovieID}/Refresh")]
    public async Task<ActionResult> RefreshMovie([FromRoute] int tvdbMovieID)
    {
        var scheduler = await _schedulerFactory.GetScheduler();
        await scheduler.StartJob<GetTvdbMovieJob>(job => job.TvdbMovieID = tvdbMovieID);
        return Ok();
    }

    /// <summary>
    /// Links a TVDB movie (by external TVDB ID) to a MediaSeries.
    /// </summary>
    [HttpPost("Movie/{tvdbMovieID}/Link/{seriesID}")]
    public ActionResult LinkMovie([FromRoute] int tvdbMovieID, [FromRoute] int seriesID)
    {
        if (RepoFactory.TVDB_Movie.GetByTvdbMovieID(tvdbMovieID) is null)
            return NotFound($"TVDB movie {tvdbMovieID} not in cache. Refresh it first.");

        var series = RepoFactory.MediaSeries.GetByID(seriesID);
        if (series is null)
            return NotFound($"MediaSeries {seriesID} not found.");

        series.TvdbMovieExternalID = tvdbMovieID;
        series.TvdbShowExternalID = null;
        RepoFactory.MediaSeries.Save(series, false, false);
        return Ok();
    }

    /// <summary>
    /// Removes the TVDB movie link from a MediaSeries.
    /// </summary>
    [HttpDelete("Movie/{tvdbMovieID}/Link/{seriesID}")]
    public ActionResult UnlinkMovie([FromRoute] int tvdbMovieID, [FromRoute] int seriesID)
    {
        var series = RepoFactory.MediaSeries.GetByID(seriesID);
        if (series is null)
            return NotFound($"MediaSeries {seriesID} not found.");

        if (series.TvdbMovieExternalID != tvdbMovieID)
            return BadRequest($"MediaSeries {seriesID} is not linked to TVDB movie {tvdbMovieID}.");

        series.TvdbMovieExternalID = null;
        RepoFactory.MediaSeries.Save(series, false, false);
        return Ok();
    }
}
