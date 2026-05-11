using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DaCollector.Abstractions.Metadata.Enums;
using DaCollector.Server.Plex;
using DaCollector.Server.Services;
using DaCollector.Server.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace DaCollector.Tests;

public class DaCollectorStatusServiceTests
{
    [Fact]
    public void GetProviderStatuses_ReportsProviderReadinessWithoutSecrets()
    {
        var settings = new ServerSettings();
        settings.TMDB.UserApiKey = "tmdb-secret";
        settings.TVDB.Enabled = true;
        settings.TVDB.ApiKey = "tvdb-secret";
        settings.TVDB.Pin = "tvdb-pin";

        var service = CreateService(settings, _ => new(HttpStatusCode.NotFound));
        var providers = service.GetProviderStatuses();

        var tmdb = providers.Single(provider => provider.Provider == ExternalProvider.TMDB);
        var tvdb = providers.Single(provider => provider.Provider == ExternalProvider.TVDB);

        Assert.Equal([ExternalProvider.TMDB, ExternalProvider.TVDB], providers.Select(provider => provider.Provider));
        Assert.True(tmdb.Ready);
        Assert.True(tmdb.CredentialConfigured);
        Assert.True(tvdb.Ready);
        Assert.True(tvdb.CredentialConfigured);
        Assert.True(tvdb.SecondaryCredentialConfigured);
        Assert.DoesNotContain(providers.SelectMany(provider => provider.Warnings), warning => warning.Contains("secret", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GetServerCapabilities_ReturnsCompletedOperationalChecklist()
    {
        var service = CreateService(new ServerSettings(), _ => new(HttpStatusCode.NotFound));

        var capabilities = service.GetServerCapabilities();
        var keys = capabilities.Select(capability => capability.Key).ToArray();

        Assert.Equal<string>(
        [
            "scan-folders",
            "hash-files",
            "parse-filenames",
            "match-files",
            "fetch-metadata",
            "store-database-records",
            "track-watched-status",
            "expose-api-endpoints",
            "serve-web-ui",
            "talk-to-plugins",
            "run-background-jobs",
        ], keys);
        Assert.All(capabilities, capability => Assert.True(capability.Completed));
        Assert.All(capabilities, capability => Assert.NotEmpty(capability.Components));
        Assert.Contains(capabilities, capability => capability.ApiRoutes.Contains("POST /api/v3/ProviderMatch/Scan"));
    }

    [Fact]
    public async Task GetPlexTargetStatus_ReadsIdentityAndLibrariesWithoutReturningToken()
    {
        var settings = new ServerSettings();
        settings.Plex.TargetBaseUrl = "http://plex.test:32400";
        settings.Plex.TargetSectionKey = "1";
        settings.Plex.TargetToken = "plex-secret";

        var service = CreateService(settings, request => request.RequestUri?.AbsolutePath switch
        {
            "/identity" => XmlResponse("<MediaContainer machineIdentifier=\"machine-1\" version=\"1.2.3\" apiVersion=\"1\" claimed=\"1\" />"),
            "/library/sections" => XmlResponse("<MediaContainer><Directory key=\"1\" title=\"Movies\" type=\"movie\" /><Directory key=\"2\" title=\"TV Shows\" type=\"show\" /></MediaContainer>"),
            _ => new(HttpStatusCode.NotFound),
        });

        var status = await service.GetPlexTargetStatus();

        Assert.True(status.Ready);
        Assert.True(status.TokenConfigured);
        Assert.Equal("1", status.SectionKey);
        Assert.Equal(2, status.LibraryCount);
        Assert.Equal("machine-1", status.Identity.MachineIdentifier);
        Assert.DoesNotContain("plex-secret", status.Identity.Status, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(status.Warnings, warning => warning.Contains("plex-secret", StringComparison.OrdinalIgnoreCase));
    }

    private static DaCollectorStatusService CreateService(ServerSettings settings, Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var settingsProvider = new Mock<ISettingsProvider>();
        settingsProvider.Setup(provider => provider.GetSettings(It.IsAny<bool>())).Returns(settings);

        var plexService = new PlexTargetService(
            settingsProvider.Object,
            new TestHttpClientFactory(new DelegateHttpMessageHandler(responder)),
            NullLogger<PlexTargetService>.Instance
        );
        return new(settingsProvider.Object, plexService);
    }

    private static HttpResponseMessage XmlResponse(string xml) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(xml),
        };

    private sealed class TestHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class DelegateHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }
}
