using DaCollector.Abstractions.Config.Services;
using DaCollector.Abstractions.Plugin;
using DaCollector.Server.Settings;
using Moq;
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
}
