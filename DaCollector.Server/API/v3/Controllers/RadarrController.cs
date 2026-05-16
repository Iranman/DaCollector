using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DaCollector.Server.API.Annotations;
using DaCollector.Server.Integrations;
using DaCollector.Server.Settings;

#nullable enable
namespace DaCollector.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize]
public class RadarrController(ISettingsProvider settingsProvider, RadarrService radarrService) : BaseController(settingsProvider)
{
    public record QualityProfileDto(int Id, string Name);
    public record RootFolderDto(int Id, string Path);
    public record RequestBody(int TmdbId);

    /// <summary>
    /// Test the configured Radarr connection.
    /// </summary>
    [HttpGet("Test")]
    public async Task<ActionResult<string>> Test()
    {
        try
        {
            return await radarrService.TestConnection();
        }
        catch (System.Exception ex)
        {
            return ValidationProblem(ex.Message, "Radarr");
        }
    }

    /// <summary>
    /// List quality profiles from Radarr.
    /// </summary>
    [HttpGet("QualityProfile")]
    public async Task<ActionResult<IReadOnlyList<QualityProfileDto>>> GetQualityProfiles()
    {
        try
        {
            var profiles = await radarrService.GetQualityProfiles();
            return Ok(profiles.Select(p => new QualityProfileDto(p.Id, p.Name)));
        }
        catch (System.Exception ex)
        {
            return ValidationProblem(ex.Message, "Radarr");
        }
    }

    /// <summary>
    /// List root folders from Radarr.
    /// </summary>
    [HttpGet("RootFolder")]
    public async Task<ActionResult<IReadOnlyList<RootFolderDto>>> GetRootFolders()
    {
        try
        {
            var folders = await radarrService.GetRootFolders();
            return Ok(folders.Select(f => new RootFolderDto(f.Id, f.Path)));
        }
        catch (System.Exception ex)
        {
            return ValidationProblem(ex.Message, "Radarr");
        }
    }

    /// <summary>
    /// Request a movie by TMDB ID.
    /// </summary>
    [Authorize("admin")]
    [HttpPost("Request")]
    public async Task<ActionResult> RequestMovie([FromBody] RequestBody body)
    {
        var result = await radarrService.RequestMovie(body.TmdbId);
        if (!result.Success)
            return ValidationProblem(result.Error ?? "Request failed.", "Radarr");
        return Ok();
    }
}
