using System;
using System.Collections.Generic;
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
public class ManagedCollectionController(
    ISettingsProvider settingsProvider,
    ManagedCollectionService collectionService,
    ManagedCollectionSyncService syncService
) : BaseController(settingsProvider)
{
    /// <summary>
    /// List managed collection definitions.
    /// </summary>
    [HttpGet]
    public ActionResult<IReadOnlyList<CollectionDefinition>> GetCollections() =>
        new(collectionService.GetAll());

    /// <summary>
    /// Get one managed collection definition.
    /// </summary>
    [HttpGet("{collectionID:guid}")]
    public ActionResult<CollectionDefinition> GetCollection([FromRoute] Guid collectionID)
    {
        var collection = collectionService.Get(collectionID);
        return collection is null ? NotFound("No managed collection exists for the given collectionID.") : collection;
    }

    /// <summary>
    /// Create a managed collection definition.
    /// </summary>
    [Authorize("admin")]
    [HttpPost]
    public ActionResult<CollectionDefinition> CreateCollection([FromBody] CollectionDefinition collection)
    {
        try
        {
            var created = collectionService.Create(collection);
            return Created($"/api/v3/ManagedCollection/{created.ID}", created);
        }
        catch (ArgumentException e)
        {
            ModelState.AddModelError(nameof(collection), e.Message);
            return ValidationProblem(ModelState);
        }
    }

    /// <summary>
    /// Replace a managed collection definition.
    /// </summary>
    [Authorize("admin")]
    [HttpPut("{collectionID:guid}")]
    public ActionResult<CollectionDefinition> PutCollection([FromRoute] Guid collectionID, [FromBody] CollectionDefinition collection)
    {
        try
        {
            var updated = collectionService.Update(collectionID, collection);
            return updated is null ? NotFound("No managed collection exists for the given collectionID.") : updated;
        }
        catch (ArgumentException e)
        {
            ModelState.AddModelError(nameof(collection), e.Message);
            return ValidationProblem(ModelState);
        }
    }

    /// <summary>
    /// Delete a managed collection definition.
    /// </summary>
    [Authorize("admin")]
    [HttpDelete("{collectionID:guid}")]
    public ActionResult DeleteCollection([FromRoute] Guid collectionID) =>
        collectionService.Delete(collectionID) ? NoContent() : NotFound("No managed collection exists for the given collectionID.");

    /// <summary>
    /// Preview a saved managed collection definition.
    /// </summary>
    [HttpPost("{collectionID:guid}/Preview")]
    public ActionResult<CollectionPreview> PreviewCollection([FromRoute] Guid collectionID)
    {
        try
        {
            var preview = collectionService.Preview(collectionID);
            return preview is null ? NotFound("No managed collection exists for the given collectionID.") : preview;
        }
        catch (ArgumentException e)
        {
            ModelState.AddModelError(nameof(collectionID), e.Message);
            return ValidationProblem(ModelState);
        }
    }

    /// <summary>
    /// Preview an unsaved managed collection definition.
    /// </summary>
    [HttpPost("Preview")]
    public ActionResult<CollectionPreview> PreviewCollection([FromBody] CollectionDefinition collection)
    {
        try
        {
            return collectionService.Preview(collection);
        }
        catch (ArgumentException e)
        {
            ModelState.AddModelError(nameof(collection), e.Message);
            return ValidationProblem(ModelState);
        }
    }

    /// <summary>
    /// Evaluate all enabled managed collection definitions.
    /// </summary>
    [Authorize("admin")]
    [HttpPost("Sync")]
    public async Task<ActionResult<CollectionSyncRunResult>> SyncCollections([FromQuery] bool apply = false, CancellationToken cancellationToken = default) =>
        await syncService.Run(apply, cancellationToken);

    /// <summary>
    /// Evaluate one managed collection definition.
    /// </summary>
    [Authorize("admin")]
    [HttpPost("{collectionID:guid}/Sync")]
    public async Task<ActionResult<CollectionSyncResult>> SyncCollection([FromRoute] Guid collectionID, [FromQuery] bool apply = false, CancellationToken cancellationToken = default)
    {
        var result = await syncService.Run(collectionID, apply, cancellationToken);
        return result is null ? NotFound("No managed collection exists for the given collectionID.") : result;
    }
}
