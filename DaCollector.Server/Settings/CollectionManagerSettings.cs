using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using DaCollector.Abstractions.Collections;

namespace DaCollector.Server.Settings;

/// <summary>
/// Settings for DaCollector managed movie and TV collections.
/// </summary>
public class CollectionManagerSettings
{
    /// <summary>
    /// Enable scheduled collection evaluation.
    /// </summary>
    [DefaultValue(false)]
    public bool ScheduledSyncEnabled { get; set; } = false;

    /// <summary>
    /// Minimum interval, in minutes, between scheduled collection sync runs.
    /// </summary>
    [Range(15, 10080)]
    public int SyncIntervalMinutes { get; set; } = 1440;

    /// <summary>
    /// Persisted managed collection definitions.
    /// </summary>
    public List<CollectionDefinition> Collections { get; set; } = [];
}
