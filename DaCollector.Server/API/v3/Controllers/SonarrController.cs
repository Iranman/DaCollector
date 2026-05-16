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
public class SonarrController(ISettingsProvider settingsProvider, SonarrService sonarrService) : BaseController(settingsProvider)
{
    public record QualityProfileDto(int Id, string Name);
    public record RootFolderDto(int Id, string Path);
    public record RequestBody(int TvdbId);

    /// <summary>
    /// Test the configured Sonarr connection.
    /// </summary>
    [HttpGet("Test")]
    public async Task<ActionResult<string>> Test()
    {
        try
        {
            return await sonarrService.TestConnection();
        }
        catch (System.Exception ex)
        {
            return ValidationProblem(ex.Message, "Sonarr");
        }
    }

    /// <summary>
    /// List quality profiles from Sonarr.
    /// </summary>
    [HttpGet("QualityProfile")]
    public async Task<ActionResult<IReadOnlyList<QualityProfileDto>>> GetQualityProfiles()
    {
        try
        {
            var profiles = await sonarrService.GetQualityProfiles();
            return Ok(profiles.Select(p => new QualityProfileDto(p.Id, p.Name)));
        }
        catch (System.Exception ex)
        {
            return ValidationProblem(ex.Message, "Sonarr");
        }
    }

    /// <summary>
    /// List root folders from Sonarr.
    /// </summary>
    [HttpGet("RootFolder")]
    public async Task<ActionResult<IReadOnlyList<RootFolderDto>>> GetRootFolders()
    {
        try
        {
            var folders = await sonarrService.GetRootFolders();
            return Ok(folders.Select(f => new RootFolderDto(f.Id, f.Path)));
        }
        catch (System.Exception ex)
        {
            return ValidationProblem(ex.Message, "Sonarr");
        }
    }

    /// <summary>
    /// Request a TV show by TVDB ID.
    /// </summary>
    [Authorize("admin")]
    [HttpPost("Request")]
    public async Task<ActionResult> RequestShow([FromBody] RequestBody body)
    {
        var result = await sonarrService.RequestShow(body.TvdbId);
        if (!result.Success)
            return ValidationProblem(result.Error ?? "Request failed.", "Sonarr");
        return Ok();
    }
}
