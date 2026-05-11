using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DaCollector.Server.API.Annotations;
using DaCollector.Server.Services;
using DaCollector.Server.Settings;

#nullable enable
namespace DaCollector.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize]
public class DaCollectorStatusController(
    ISettingsProvider settingsProvider,
    DaCollectorStatusService statusService
) : BaseController(settingsProvider)
{
    /// <summary>
    /// Return DaCollector provider, collection-manager, and Plex target readiness.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<DaCollectorStatus>> GetStatus(CancellationToken cancellationToken = default) =>
        await statusService.GetStatus(cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Return provider configuration readiness without exposing provider secrets.
    /// </summary>
    [HttpGet("Providers")]
    public ActionResult<IReadOnlyList<ProviderConnectionStatus>> GetProviders() =>
        new(statusService.GetProviderStatuses());

    /// <summary>
    /// Return the server capability checklist used by the Web UI and operators.
    /// </summary>
    [HttpGet("Capabilities")]
    public ActionResult<IReadOnlyList<ServerCapabilityStatus>> GetCapabilities() =>
        new(statusService.GetServerCapabilities());

    /// <summary>
    /// Return Plex target readiness without exposing the configured Plex token.
    /// </summary>
    [HttpGet("Plex")]
    public async Task<ActionResult<PlexTargetConnectionStatus>> GetPlex(CancellationToken cancellationToken = default) =>
        await statusService.GetPlexTargetStatus(cancellationToken).ConfigureAwait(false);
}
