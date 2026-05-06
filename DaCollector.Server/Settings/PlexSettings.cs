using System.Collections.Generic;
using System.ComponentModel;
using DaCollector.Abstractions.Config.Attributes;
using DaCollector.Abstractions.Config.Enums;

namespace DaCollector.Server.Settings;

public class PlexSettings
{
    /// <summary>
    /// Direct Plex server URL used by DaCollector collection sync target.
    /// </summary>
    public string TargetBaseUrl { get; set; } = "http://127.0.0.1:32400";

    /// <summary>
    /// Plex library section key used by DaCollector collection sync target.
    /// </summary>
    public string TargetSectionKey { get; set; } = string.Empty;

    /// <summary>
    /// Plex token used by DaCollector direct collection sync target.
    /// </summary>
    [Visibility(DisplayVisibility.Hidden)]
    [PasswordPropertyText]
    public string TargetToken { get; set; } = string.Empty;

    public List<int> Libraries { get; set; } = [];

    public string Server { get; set; } = string.Empty;
}
