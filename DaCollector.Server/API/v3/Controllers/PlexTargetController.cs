using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DaCollector.Abstractions.Collections;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DaCollector.Abstractions.MediaServers.Plex;
using DaCollector.Server.API.Annotations;
using DaCollector.Server.Collections;
using DaCollector.Server.Plex;
using DaCollector.Server.Settings;

#nullable enable
namespace DaCollector.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize]
public class PlexTargetController(
    ISettingsProvider settingsProvider,
    PlexTargetService plexTargetService,
    ManagedCollectionService collectionService
) : BaseController(settingsProvider)
{
    /// <summary>
    /// Test the configured Plex target identity endpoint.
    /// </summary>
    [HttpGet("Identity")]
    public async Task<ActionResult<PlexServerIdentity>> GetIdentity() =>
        await plexTargetService.GetIdentity();

    /// <summary>
    /// Test a Plex target identity endpoint without saving settings.
    /// </summary>
    [HttpPost("Identity")]
    public async Task<ActionResult<PlexServerIdentity>> TestIdentity([FromBody] PlexTargetConnectionBody body) =>
        await plexTargetService.GetIdentity(body.BaseUrl);

    /// <summary>
    /// List libraries from the configured Plex target.
    /// </summary>
    [HttpGet("Library")]
    public async Task<ActionResult<IReadOnlyList<PlexLibrarySection>>> GetLibraries()
    {
        try
        {
            return new(await plexTargetService.GetLibraries());
        }
        catch (InvalidOperationException e)
        {
            return ValidationProblem(e.Message, "Plex");
        }
    }

    /// <summary>
    /// List libraries from a Plex target without saving settings.
    /// </summary>
    [HttpPost("Library")]
    public async Task<ActionResult<IReadOnlyList<PlexLibrarySection>>> TestLibraries([FromBody] PlexTargetConnectionBody body)
    {
        try
        {
            return new(await plexTargetService.GetLibraries(body.BaseUrl, body.Token));
        }
        catch (InvalidOperationException e)
        {
            return ValidationProblem(e.Message, "Plex");
        }
    }

    /// <summary>
    /// List media items from a configured Plex library section.
    /// </summary>
    [HttpGet("Library/{sectionKey}/Item")]
    public async Task<ActionResult<IReadOnlyList<PlexMediaItem>>> GetLibraryItems([FromRoute] string sectionKey)
    {
        try
        {
            return new(await plexTargetService.GetLibraryItems(sectionKey));
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException)
        {
            return ValidationProblem(e.Message, "Plex");
        }
    }

    /// <summary>
    /// List media items from a Plex library section without saving settings.
    /// </summary>
    [HttpPost("Library/{sectionKey}/Item")]
    public async Task<ActionResult<IReadOnlyList<PlexMediaItem>>> TestLibraryItems([FromRoute] string sectionKey, [FromBody] PlexTargetConnectionBody body)
    {
        try
        {
            return new(await plexTargetService.GetLibraryItems(sectionKey, body.BaseUrl, body.Token));
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException)
        {
            return ValidationProblem(e.Message, "Plex");
        }
    }

    /// <summary>
    /// Match a managed collection definition against a Plex library section.
    /// </summary>
    [HttpPost("Library/{sectionKey}/Match")]
    public async Task<ActionResult<PlexCollectionMatch>> MatchCollection([FromRoute] string sectionKey, [FromBody] PlexTargetCollectionMatchBody body)
    {
        try
        {
            var preview = collectionService.Preview(body.Collection);
            return new(await plexTargetService.MatchItems(sectionKey, preview.Items, body.BaseUrl, body.Token));
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException)
        {
            return ValidationProblem(e.Message, "Plex");
        }
    }

    /// <summary>
    /// Apply a managed collection definition to a Plex library section.
    /// </summary>
    [Authorize("admin")]
    [HttpPost("Library/{sectionKey}/Apply")]
    public async Task<ActionResult<PlexCollectionApplyResult>> ApplyCollection([FromRoute] string sectionKey, [FromBody] PlexTargetCollectionMatchBody body)
    {
        try
        {
            var preview = collectionService.Preview(body.Collection);
            return new(await plexTargetService.ApplyCollection(
                sectionKey,
                preview.Collection.Name,
                preview.Items,
                preview.Collection.SyncMode,
                body.BaseUrl,
                body.Token
            ));
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException)
        {
            return ValidationProblem(e.Message, "Plex");
        }
    }

    public record PlexTargetConnectionBody
    {
        public string? BaseUrl { get; init; }

        public string? Token { get; init; }
    }

    public sealed record PlexTargetCollectionMatchBody : PlexTargetConnectionBody
    {
        public CollectionDefinition Collection { get; init; } = new();
    }
}
