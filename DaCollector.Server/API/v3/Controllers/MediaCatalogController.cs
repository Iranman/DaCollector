using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DaCollector.Abstractions.Metadata.Enums;
using DaCollector.Server.API.Annotations;
using DaCollector.Server.API.v3.Helpers;
using DaCollector.Server.API.v3.Models.Common;
using DaCollector.Server.API.v3.Models.MediaCatalog;
using DaCollector.Server.Media;
using DaCollector.Server.Settings;

#nullable enable
namespace DaCollector.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize]
public class MediaCatalogController(
    ISettingsProvider settingsProvider,
    MediaCatalogService catalogService
) : BaseController(settingsProvider)
{
    /// <summary>
    /// List locally cached movies and TV shows as generic DaCollector catalog items.
    /// </summary>
    [HttpGet]
    public ActionResult<ListResult<MediaCatalogItem>> GetItems(
        [FromQuery] MediaKind kind = MediaKind.Unknown,
        [FromQuery] string? search = null,
        [FromQuery] bool fuzzy = true,
        [FromQuery] bool includeRestricted = false,
        [FromQuery] bool includeVideos = false,
        [FromQuery, Range(0, 1000)] int pageSize = 50,
        [FromQuery, Range(1, int.MaxValue)] int page = 1
    )
    {
        if (kind is not MediaKind.Unknown and not MediaKind.Movie and not MediaKind.Show)
        {
            ModelState.AddModelError(nameof(kind), "Media catalog supports Movie, Show, or Unknown for all items.");
            return ValidationProblem(ModelState);
        }

        return catalogService
            .GetItems(kind, search, fuzzy, includeRestricted, includeVideos)
            .ToListResult(page, pageSize);
    }

    /// <summary>
    /// List locally cached movies as generic DaCollector catalog items.
    /// </summary>
    [HttpGet("Movies")]
    public ActionResult<ListResult<MediaCatalogItem>> GetMovies(
        [FromQuery] string? search = null,
        [FromQuery] bool fuzzy = true,
        [FromQuery] bool includeRestricted = false,
        [FromQuery] bool includeVideos = false,
        [FromQuery, Range(0, 1000)] int pageSize = 50,
        [FromQuery, Range(1, int.MaxValue)] int page = 1
    ) =>
        catalogService
            .GetItems(MediaKind.Movie, search, fuzzy, includeRestricted, includeVideos)
            .ToListResult(page, pageSize);

    /// <summary>
    /// List locally cached TV shows as generic DaCollector catalog items.
    /// </summary>
    [HttpGet("Shows")]
    public ActionResult<ListResult<MediaCatalogItem>> GetShows(
        [FromQuery] string? search = null,
        [FromQuery] bool fuzzy = true,
        [FromQuery] bool includeRestricted = false,
        [FromQuery, Range(0, 1000)] int pageSize = 50,
        [FromQuery, Range(1, int.MaxValue)] int page = 1
    ) =>
        catalogService
            .GetItems(MediaKind.Show, search, fuzzy, includeRestricted, includeVideos: false)
            .ToListResult(page, pageSize);
}
