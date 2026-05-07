using System;
using System.IO;
using DaCollector.Abstractions.Config.Services;
using DaCollector.Abstractions.Plugin;
using DaCollector.Server.Settings;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace DaCollector.Tests;

public class ServerSettingsTests
{
    [Fact]
    public void Validate_AllowsCompletingFirstRunWithoutLegacyAniDbCredentials()
    {
        var settings = new ServerSettings { FirstRun = false };

        var errors = ServerSettings.Validate(
            settings,
            Mock.Of<IConfigurationService>(),
            Mock.Of<IPluginManager>()
        );

        Assert.Empty(errors);
    }

    [Fact]
    public void WebSettings_DefaultPortIs38111()
    {
        var settings = new ServerSettings();

        Assert.Equal(38111, settings.Web.Port);
    }

    [Fact]
    public void ApplyMigrations_MovesOldDefaultWebPortTo38111()
    {
        var migrated = ApplyMigrations("""{"SettingsVersion":14,"FirstRun":true,"Web":{"Port":8111}}""");
        var json = JObject.Parse(migrated);

        Assert.Equal(38111, json["Web"]!["Port"]!.Value<int>());
    }

    [Fact]
    public void ApplyMigrations_PreservesCustomWebPort()
    {
        var migrated = ApplyMigrations("""{"SettingsVersion":14,"FirstRun":true,"Web":{"Port":28111}}""");
        var json = JObject.Parse(migrated);

        Assert.Equal(28111, json["Web"]!["Port"]!.Value<int>());
    }

    private static string ApplyMigrations(string settings)
    {
        var dataPath = Path.Combine(Path.GetTempPath(), $"dacollector-settings-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dataPath);
        try
        {
            return ServerSettings.ApplyMigrations(settings, new TestApplicationPaths(dataPath));
        }
        finally
        {
            Directory.Delete(dataPath, recursive: true);
        }
    }

    private sealed class TestApplicationPaths(string dataPath) : IApplicationPaths
    {
        public string ApplicationPath => dataPath;

        public string WebPath => dataPath;

        public string DataPath => dataPath;

        public string ImagesPath => dataPath;

        public string PluginsPath => dataPath;

        public string ConfigurationsPath => dataPath;

        public string LogsPath => dataPath;
    }
}
