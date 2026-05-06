using System.ComponentModel;
using DaCollector.Abstractions.Config.Attributes;
using DaCollector.Abstractions.Config.Enums;
using DaCollector.Server.Server;

namespace DaCollector.Server.Settings;

public class TraktSettings
{
    public bool Enabled { get; set; } = false;

    [Visibility(DisplayVisibility.Hidden)]
    [PasswordPropertyText]
    public string AuthToken { get; set; } = string.Empty;

    [Visibility(DisplayVisibility.Hidden)]
    [PasswordPropertyText]
    public string RefreshToken { get; set; } = string.Empty;

    [Visibility(DisplayVisibility.ReadOnly)]
    public string TokenExpirationDate { get; set; } = string.Empty;

    public ScheduledUpdateFrequency SyncFrequency { get; set; } = ScheduledUpdateFrequency.Daily;
}
