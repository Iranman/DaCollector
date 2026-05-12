using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DaCollector.Server.API.Annotations;
using DaCollector.Server.API.v3.Helpers;
using DaCollector.Server.API.v3.Models.Common;
using DaCollector.Server.API.v3.Models.Media;
using DaCollector.Server.Media;
using DaCollector.Server.Settings;

#nullable enable
namespace DaCollector.Server.API.v3.Controllers;

/// <summary>
/// Generic DaCollector media read API for movies, TV shows, seasons, episodes, and local files.
/// </summary>
[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize]
public class MediaController(
    ISettingsProvider settingsProvider,
    MediaReadService mediaReadService
) : BaseController(settingsProvider)
{
    /// <summary>
    /// List locally cached movies from TMDB and/or TVDB.
    /// </summary>
    [HttpGet("Movies")]
    public ActionResult<ListResult<MediaMovieDto>> GetMovies(
        [FromQuery] string provider = "tmdb",
        [FromQuery] string? search = null,
        [FromQuery] bool includeRestricted = false,
        [FromQuery] bool includeVideos = false,
        [FromQuery, Range(0, 1000)] int pageSize = 50,
        [FromQuery, Range(1, int.MaxValue)] int page = 1
    )
    {
        if (!ValidateProvider(provider, allowAll: true))
            return ValidationProblem(ModelState);

        return mediaReadService
            .GetMovies(provider, search, includeRestricted, includeVideos)
            .ToListResult(page, pageSize);
    }

    /// <summary>
    /// Get one locally cached movie by provider and provider ID.
    /// </summary>
    [HttpGet("Movies/{provider}/{providerID}")]
    public ActionResult<MediaMovieDto> GetMovie(
        [FromRoute] string provider,
        [FromRoute, Range(1, int.MaxValue)] int providerID
    )
    {
        if (!ValidateProvider(provider, allowAll: false))
            return ValidationProblem(ModelState);

        return mediaReadService.GetMovie(provider, providerID) is { } movie ? Ok(movie) : NotFound();
    }

    /// <summary>
    /// List locally cached TV shows from TMDB and/or TVDB.
    /// </summary>
    [HttpGet("Shows")]
    public ActionResult<ListResult<MediaShowDto>> GetShows(
        [FromQuery] string provider = "all",
        [FromQuery] string? search = null,
        [FromQuery] bool includeRestricted = false,
        [FromQuery, Range(0, 1000)] int pageSize = 50,
        [FromQuery, Range(1, int.MaxValue)] int page = 1
    )
    {
        if (!ValidateProvider(provider, allowAll: true))
            return ValidationProblem(ModelState);

        return mediaReadService
            .GetShows(provider, search, includeRestricted)
            .ToListResult(page, pageSize);
    }

    /// <summary>
    /// Get one locally cached TV show by provider and provider ID.
    /// </summary>
    [HttpGet("Shows/{provider}/{providerID}")]
    public ActionResult<MediaShowDto> GetShow(
        [FromRoute] string provider,
        [FromRoute, Range(1, int.MaxValue)] int providerID
    )
    {
        if (!ValidateProvider(provider, allowAll: false))
            return ValidationProblem(ModelState);

        return mediaReadService.GetShow(provider, providerID) is { } show ? Ok(show) : NotFound();
    }

    /// <summary>
    /// List seasons for one locally cached TV show.
    /// </summary>
    [HttpGet("Shows/{provider}/{providerID}/Seasons")]
    public ActionResult<IReadOnlyList<MediaSeasonDto>> GetShowSeasons(
        [FromRoute] string provider,
        [FromRoute, Range(1, int.MaxValue)] int providerID
    )
    {
        if (!ValidateProvider(provider, allowAll: false))
            return ValidationProblem(ModelState);

        return mediaReadService.GetShowSeasons(provider, providerID) is { } seasons ? Ok(seasons) : NotFound();
    }

    /// <summary>
    /// List episodes for one locally cached TV show.
    /// </summary>
    [HttpGet("Shows/{provider}/{providerID}/Episodes")]
    public ActionResult<IReadOnlyList<MediaEpisodeDto>> GetShowEpisodes(
        [FromRoute] string provider,
        [FromRoute, Range(1, int.MaxValue)] int providerID,
        [FromQuery, Range(0, int.MaxValue)] int? seasonNumber = null
    )
    {
        if (!ValidateProvider(provider, allowAll: false))
            return ValidationProblem(ModelState);

        return mediaReadService.GetShowEpisodes(provider, providerID, seasonNumber) is { } episodes ? Ok(episodes) : NotFound();
    }

    /// <summary>
    /// List local media files from the scanner inventory.
    /// </summary>
    [HttpGet("Files")]
    public ActionResult<ListResult<MediaFileDto>> GetFiles(
        [FromQuery] string? search = null,
        [FromQuery] bool includeIgnored = false,
        [FromQuery] bool includeReview = false,
        [FromQuery] bool includeAbsolutePaths = false,
        [FromQuery, Range(0, 1000)] int pageSize = 50,
        [FromQuery, Range(1, int.MaxValue)] int page = 1
    )
        => mediaReadService
            .GetFiles(search, includeIgnored, includeReview, includeAbsolutePaths)
            .ToListResult(page, pageSize);

    /// <summary>
    /// Get one local media file from the scanner inventory.
    /// </summary>
    [HttpGet("Files/{fileID}")]
    public ActionResult<MediaFileDto> GetFile(
        [FromRoute, Range(1, int.MaxValue)] int fileID,
        [FromQuery] bool includeReview = true,
        [FromQuery] bool includeAbsolutePaths = false
    )
        => mediaReadService.GetFile(fileID, includeReview, includeAbsolutePaths) is { } file ? Ok(file) : NotFound();

    /// <summary>
    /// Find local media files whose path ends with the given suffix.
    /// </summary>
    [HttpGet("Files/PathEndsWith")]
    public ActionResult<IReadOnlyList<MediaFileDto>> GetFilesByPathEndsWithQuery(
        [FromQuery] string tail,
        [FromQuery] bool includeReview = true,
        [FromQuery] bool includeAbsolutePaths = false
    )
    {
        if (string.IsNullOrWhiteSpace(tail))
            return Ok(Array.Empty<MediaFileDto>());

        return Ok(mediaReadService.GetFilesByPathEndsWith(tail, includeReview, includeAbsolutePaths));
    }

    /// <summary>
    /// Find local media files whose path ends with the given suffix (path-segment form).
    /// </summary>
    [HttpGet("Files/PathEndsWith/{*tail}")]
    public ActionResult<IReadOnlyList<MediaFileDto>> GetFilesByPathEndsWith(
        [FromRoute] string tail,
        [FromQuery] bool includeReview = true,
        [FromQuery] bool includeAbsolutePaths = false
    )
    {
        if (string.IsNullOrWhiteSpace(tail))
            return Ok(Array.Empty<MediaFileDto>());

        return Ok(mediaReadService.GetFilesByPathEndsWith(
            Uri.UnescapeDataString(tail), includeReview, includeAbsolutePaths));
    }

    private bool ValidateProvider(string provider, bool allowAll)
    {
        if (MediaReadService.IsValidProvider(provider, allowAll))
            return true;

        ModelState.AddModelError(nameof(provider), allowAll
            ? "Provider must be tmdb, tvdb, or all."
            : "Provider must be tmdb or tvdb.");
        return false;
    }
}
