
using System.IO;
using System.Linq;
using DaCollector.Abstractions.Video;
using DaCollector.Server.Settings;

namespace DaCollector.Server.Plugin;

public class CoreIgnoreRule(ISettingsProvider settingsProvider) : IManagedFolderIgnoreRule
{
    public string Name { get; } = "Built-In Ignore Rule";

    public bool ShouldIgnore(IManagedFolder folder, FileSystemInfo fileInfo)
    {
        if (fileInfo is not FileInfo) return false;
        var exclusions = settingsProvider.GetSettings().Import.ExcludeExpressions;
        return exclusions.Any(r => r.IsMatch(fileInfo.FullName));
    }
}
