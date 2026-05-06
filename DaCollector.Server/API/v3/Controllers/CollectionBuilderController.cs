using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DaCollector.Abstractions.Collections;
using DaCollector.Server.API.Annotations;
using DaCollector.Server.Collections;
using DaCollector.Server.Settings;

namespace DaCollector.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize]
public class CollectionBuilderController(
    ISettingsProvider settingsProvider,
    CollectionBuilderPreviewService previewService
) : BaseController(settingsProvider)
{
    /// <summary>
    /// List collection builders supported by DaCollector.
    /// </summary>
    [HttpGet]
    public ActionResult<IReadOnlyList<CollectionBuilderDescriptor>> GetBuilders() =>
        CollectionBuilderCatalog.All.Values
            .OrderBy(builder => builder.Name)
            .ToList();

    /// <summary>
    /// Preview the local output for a collection builder rule.
    /// </summary>
    [HttpPost("Preview")]
    public async Task<ActionResult<CollectionBuilderPreview>> Preview([FromBody] CollectionRule rule, CancellationToken cancellationToken = default)
    {
        try
        {
            return await previewService.Preview(rule, cancellationToken).ConfigureAwait(false);
        }
        catch (ArgumentException e)
        {
            ModelState.AddModelError(nameof(rule.Builder), e.Message);
            return ValidationProblem(ModelState);
        }
    }
}
