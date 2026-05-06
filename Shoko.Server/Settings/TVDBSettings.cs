using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Shoko.Abstractions.Config.Attributes;
using Shoko.Abstractions.Config.Enums;

#nullable enable
namespace Shoko.Server.Settings;

/// <summary>
/// TVDB provider settings for movie and TV collection management.
/// </summary>
public class TVDBSettings
{
    /// <summary>
    /// Enable TVDB-based matching and collection builders.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Optional TVDB API key.
    /// </summary>
    [Badge("Advanced", Theme = DisplayColorTheme.Primary)]
    [Visibility(Advanced = true)]
    [EnvironmentVariable("TVDB_API_KEY")]
    [RequiresRestart]
    [PasswordPropertyText]
    public string? ApiKey { get; set; } = null;

    /// <summary>
    /// Optional TVDB subscriber PIN.
    /// </summary>
    [Badge("Advanced", Theme = DisplayColorTheme.Primary)]
    [Visibility(Advanced = true)]
    [EnvironmentVariable("TVDB_PIN")]
    [RequiresRestart]
    [PasswordPropertyText]
    public string? Pin { get; set; } = null;

    /// <summary>
    /// Number of days to keep cached TVDB provider data.
    /// </summary>
    [Badge("Advanced", Theme = DisplayColorTheme.Primary)]
    [Visibility(Advanced = true)]
    [Range(1, 365)]
    [EnvironmentVariable("TVDB_CACHE_EXPIRATION_DAYS")]
    public int CacheExpirationDays { get; set; } = 7;
}
