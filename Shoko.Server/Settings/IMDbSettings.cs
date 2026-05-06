using System.ComponentModel.DataAnnotations;
using Shoko.Abstractions.Config.Attributes;
using Shoko.Abstractions.Config.Enums;

#nullable enable
namespace Shoko.Server.Settings;

/// <summary>
/// IMDb provider settings for movie and TV collection management.
/// </summary>
public class IMDbSettings
{
    /// <summary>
    /// Enable IMDb-based matching and collection builders.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Optional path to local IMDb dataset files.
    /// </summary>
    [Badge("Advanced", Theme = DisplayColorTheme.Primary)]
    [Visibility(Advanced = true)]
    [EnvironmentVariable("IMDB_DATASET_PATH")]
    public string DatasetPath { get; set; } = string.Empty;

    /// <summary>
    /// Number of days to keep cached IMDb provider data.
    /// </summary>
    [Badge("Advanced", Theme = DisplayColorTheme.Primary)]
    [Visibility(Advanced = true)]
    [Range(1, 365)]
    [EnvironmentVariable("IMDB_CACHE_EXPIRATION_DAYS")]
    public int CacheExpirationDays { get; set; } = 7;
}
