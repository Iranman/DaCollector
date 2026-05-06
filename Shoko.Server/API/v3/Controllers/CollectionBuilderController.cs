using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Abstractions.Collections;
using Shoko.Server.API.Annotations;
using Shoko.Server.Collections;
using Shoko.Server.Settings;

namespace Shoko.Server.API.v3.Controllers;

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
    /// List collection builders supported by The Collector.
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
    public ActionResult<CollectionBuilderPreview> Preview([FromBody] CollectionRule rule)
    {
        try
        {
            return previewService.Preview(rule);
        }
        catch (ArgumentException e)
        {
            ModelState.AddModelError(nameof(rule.Builder), e.Message);
            return ValidationProblem(ModelState);
        }
    }
}
