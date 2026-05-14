using System.Collections.Generic;
using System.Threading.Tasks;

namespace DaCollector.Abstractions.Theme.Services;

/// <summary>
/// Service for managing web UI themes. Exposed in Abstractions so plugins can
/// read or install themes without depending on server internals.
/// </summary>
public interface IThemeService
{
    /// <summary>Returns all installed themes.</summary>
    IEnumerable<ThemeInfo> GetThemes(bool forceRefresh = false);

    /// <summary>Returns a single theme by ID, or <see langword="null"/> if not found.</summary>
    ThemeInfo? GetTheme(string themeId, bool forceRefresh = false);

    /// <summary>
    /// Removes a theme from disk. Returns <see langword="true"/> if the theme
    /// existed and was deleted.
    /// </summary>
    bool RemoveTheme(ThemeInfo theme);

    /// <summary>Downloads and applies the latest version of an online theme.</summary>
    Task<ThemeInfo> UpdateThemeOnline(ThemeInfo theme, bool preview = false);

    /// <summary>Downloads and installs a theme from a URL.</summary>
    Task<ThemeInfo> InstallThemeFromUrl(string url, bool preview = false);

    /// <summary>Installs or updates a theme from a JSON definition string.</summary>
    Task<ThemeInfo> InstallOrUpdateThemeFromJson(string? content, string fileName, bool preview = false);

    /// <summary>Installs or updates a theme from raw CSS content.</summary>
    Task<ThemeInfo> CreateOrUpdateThemeFromCss(string content, string fileName, bool preview = false);
}
