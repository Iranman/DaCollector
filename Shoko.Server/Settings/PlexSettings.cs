using System.Collections.Generic;
using System.ComponentModel;
using Shoko.Abstractions.Config.Attributes;
using Shoko.Abstractions.Config.Enums;

namespace Shoko.Server.Settings;

public class PlexSettings
{
    /// <summary>
    /// Direct Plex server URL used by The Collector collection sync target.
    /// </summary>
    public string TargetBaseUrl { get; set; } = "http://127.0.0.1:32400";

    /// <summary>
    /// Plex library section key used by The Collector collection sync target.
    /// </summary>
    public string TargetSectionKey { get; set; } = string.Empty;

    /// <summary>
    /// Plex token used by The Collector direct collection sync target.
    /// </summary>
    [Visibility(DisplayVisibility.Hidden)]
    [PasswordPropertyText]
    public string TargetToken { get; set; } = string.Empty;

    public List<int> Libraries { get; set; } = [];

    public string Server { get; set; } = string.Empty;
}
