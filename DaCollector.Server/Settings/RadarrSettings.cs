using System.ComponentModel;
using DaCollector.Abstractions.Config.Attributes;
using DaCollector.Abstractions.Config.Enums;

namespace DaCollector.Server.Settings;

public class RadarrSettings
{
    public bool Enabled { get; set; } = false;

    public string BaseUrl { get; set; } = string.Empty;

    [PasswordPropertyText]
    public string ApiKey { get; set; } = string.Empty;

    public int QualityProfileId { get; set; } = 0;

    public string RootFolderPath { get; set; } = string.Empty;
}
