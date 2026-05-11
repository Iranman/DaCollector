using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using DaCollector.Server.API.Annotations;
using DaCollector.Server.Parsing;
using DaCollector.Server.Settings;

#nullable enable
namespace DaCollector.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize]
public class ParserController(
    ISettingsProvider settingsProvider,
    FilenameParserService filenameParser
) : BaseController(settingsProvider)
{
    /// <summary>
    /// Parse a movie or TV filename/path into DaCollector's first-pass media guess.
    /// </summary>
    [HttpPost("Filename")]
    public ActionResult<ParsedFilenameResult> ParseFilename(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] ParseFilenameBody body
    )
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        return filenameParser.Parse(body.Path);
    }

    /// <summary>
    /// Parse a movie or TV filename/path into DaCollector's first-pass media guess.
    /// </summary>
    [HttpGet("Filename")]
    public ActionResult<ParsedFilenameResult> ParseFilename([FromQuery, Required] string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return ValidationProblem("Path is required.", nameof(path));

        return filenameParser.Parse(path);
    }
}

public sealed record ParseFilenameBody
{
    [Required]
    public string Path { get; init; } = string.Empty;
}
