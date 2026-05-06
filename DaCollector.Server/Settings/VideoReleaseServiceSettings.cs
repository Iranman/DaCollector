using System;
using System.Collections.Generic;
using DaCollector.Abstractions.Config;
using DaCollector.Abstractions.Config.Attributes;
using DaCollector.Abstractions.Config.Enums;
using DaCollector.Server.Services;

namespace DaCollector.Server.Settings;

/// <summary>
/// Settings for the <see cref="VideoReleaseService"/>.
/// <br/>
/// These are separate from the <see cref="ServerSettings"/> to prevent
/// clients from modifying them through the settings endpoint.
/// </summary>
public class VideoReleaseServiceSettings : INewtonsoftJsonConfiguration, IHiddenConfiguration
{
    /// <summary>
    /// Whether or not to use parallel mode for the service.
    /// </summary>
    public bool ParallelMode { get; set; } = false;

    /// <summary>
    /// A dictionary containing the enabled state of each provider by id.
    /// </summary>
    [Visibility(DisplayVisibility.ReadOnly)]
    public Dictionary<Guid, bool> Enabled { get; set; } = [];

    /// <summary>
    /// A list of provider ids in order of priority.
    /// </summary>
    [Visibility(DisplayVisibility.ReadOnly)]
    public List<Guid> Priority { get; set; } = [];
}
