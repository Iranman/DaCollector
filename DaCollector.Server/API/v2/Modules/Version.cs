using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using DaCollector.Abstractions.Web.Attributes;
using DaCollector.Server.API.v2.Models.core;
using DaCollector.Server.API.v3.Controllers;
using DaCollector.Server.Settings;

#nullable enable
namespace DaCollector.Server.API.v2.Modules;

[ApiController]
[Route("/api/version")]
[ApiVersion("2.0")]
[InitFriendly]
[DatabaseBlockedExempt]
public class VersionController(ISettingsProvider settingsProvider, InitController init) : BaseController(settingsProvider)
{
    /// <summary>
    /// Return current version of DaCollector and several modules
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    public List<ComponentVersion> GetVersion()
        => init.GetVersion() is not { } versionSet ? [] : [
            new() { name = "server", version = versionSet.Server.Version },
            new() { name = "commons", version = null },
            new() { name = "models", version = null },
            new() { name = "MediaInfo", version = versionSet.MediaInfo?.Version },
            new() { name = "webui", version = versionSet.WebUI?.Version },
        ];
}
