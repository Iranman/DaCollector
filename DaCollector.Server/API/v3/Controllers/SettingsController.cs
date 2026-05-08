using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using DaCollector.Abstractions.Config.Exceptions;
using DaCollector.Abstractions.Web.Attributes;
using DaCollector.Server.API.Annotations;
using DaCollector.Server.API.v3.Models.Common;
using DaCollector.Server.Settings;
using DaCollector.Server.Utilities;

#pragma warning disable CA1822
namespace DaCollector.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize(Roles = "admin,init")]
[DatabaseBlockedExempt]
[InitFriendly]
public class SettingsController(ISettingsProvider settingsProvider) : BaseController(settingsProvider)
{
    // As far as I can tell, only GET and PATCH should be supported, as we don't support unset settings.
    // Some may be patched to "", though.

    // TODO some way of distinguishing what a normal user vs an admin can set.

    /// <summary>
    /// Get DaCollector WebUI settings.
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    public ActionResult<DaCollectorWebUISettings> GetSettings()
        => DaCollectorWebUISettings.From(SettingsProvider.GetSettings());

    /// <summary>
    /// JsonPatch the settings
    /// </summary>
    /// <param name="settings">JsonPatch operations</param>
    /// <returns></returns>
    [HttpPatch]
    public ActionResult SetSettings([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] JsonPatchDocument<ServerSettings> settings)
    {
        try
        {
            var existingSettings = (ServerSettings)SettingsProvider.GetSettings(copy: true);
            settings.ApplyTo(existingSettings, ModelState);
            SettingsProvider.SaveSettings(existingSettings);
            return Ok();
        }
        catch (ConfigurationValidationException ex)
        {
            return ValidationProblem(ex.ValidationErrors);
        }
    }

    /// <summary>
    /// Get a list of all supported languages.
    /// </summary>
    /// <returns>A list of all supported languages.</returns>
    [HttpGet("SupportedLanguages")]
    public ActionResult<List<LanguageDetails>> GetAllSupportedLanguages() =>
        Languages.AllNamingLanguages.Select(a => new LanguageDetails(a.Language)).ToList();
}

public sealed record DaCollectorWebUISettings
{
    public required bool AutoGroupSeries { get; init; }

    public required List<string> AutoGroupSeriesRelationExclusions { get; init; }

    public required bool AutoGroupSeriesUseScoreAlgorithm { get; init; }

    public required bool FileQualityFilterEnabled { get; init; }

    public required ImportSettings Import { get; init; }

    public required TMDBSettings TMDB { get; init; }

    public required TVDBSettings TVDB { get; init; }

    public required DatabaseSettings Database { get; init; }

    public required LanguageSettings Language { get; init; }

    public required PlexSettings Plex { get; init; }

    public required TraktSettings TraktTv { get; init; }

    public required CollectionManagerSettings CollectionManager { get; init; }

    public static DaCollectorWebUISettings From(IServerSettings settings) => new()
    {
        AutoGroupSeries = settings.AutoGroupSeries,
        AutoGroupSeriesRelationExclusions = settings.AutoGroupSeriesRelationExclusions,
        AutoGroupSeriesUseScoreAlgorithm = settings.AutoGroupSeriesUseScoreAlgorithm,
        FileQualityFilterEnabled = settings.FileQualityFilterEnabled,
        Import = settings.Import,
        TMDB = settings.TMDB,
        TVDB = settings.TVDB,
        Database = settings.Database,
        Language = settings.Language,
        Plex = settings.Plex,
        TraktTv = settings.TraktTv,
        CollectionManager = settings.CollectionManager,
    };
}
