using Microsoft.AspNetCore.Mvc;
using DaCollector.Abstractions.Web.Attributes;
using DaCollector.Server.API.ActionConstraints;
using DaCollector.Server.Settings;

namespace DaCollector.Server.API.v0.Controllers;

[Route("/")]
[ApiVersionNeutral]
[InitFriendly]
[DatabaseBlockedExempt]
public class IndexRedirectController(ISettingsProvider settingsProvider) : Controller
{
    [HttpGet]
    [RedirectConstraint]
    public ActionResult Index()
        => Redirect(settingsProvider.GetSettings().Web.WebUIPublicPath);
}
