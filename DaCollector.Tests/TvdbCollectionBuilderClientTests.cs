using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DaCollector.Abstractions.Metadata.Enums;
using DaCollector.Server.Collections;
using DaCollector.Server.Settings;
using Moq;
using Xunit;

#nullable enable
namespace DaCollector.Tests;

public class TvdbCollectionBuilderClientTests
{
    [Fact]
    public async Task GetMovie_ReturnsTitle_WhenResponseHasDataProperty()
    {
        var client = CreateClient(settings => settings.TVDB.ApiKey = "test-key", request =>
            request.RequestUri?.AbsolutePath switch
            {
                "/login" => LoginResponse("token"),
                _ => JsonResponse("""{"data":{"id":123,"name":"Blade Runner","type":"movie"}}"""),
            });

        var title = await client.GetMovie(123);

        Assert.NotNull(title);
        Assert.Equal(123, title.Id);
        Assert.Equal("Blade Runner", title.Title);
        Assert.Equal(MediaKind.Movie, title.Kind);
    }

    [Fact]
    public async Task GetMovie_ReturnsNull_WhenResponseMissingDataProperty()
    {
        var client = CreateClient(settings => settings.TVDB.ApiKey = "test-key", request =>
            request.RequestUri?.AbsolutePath switch
            {
                "/login" => LoginResponse("token"),
                _ => JsonResponse("""{"status":"success"}"""),
            });

        var title = await client.GetMovie(999);

        Assert.Null(title);
    }

    [Fact]
    public async Task GetMovie_ReturnsNull_WhenStatusCodeIs404()
    {
        var client = CreateClient(settings => settings.TVDB.ApiKey = "test-key", request =>
            request.RequestUri?.AbsolutePath switch
            {
                "/login" => LoginResponse("token"),
                _ => new(HttpStatusCode.NotFound),
            });

        var title = await client.GetMovie(404);

        Assert.Null(title);
    }

    [Fact]
    public async Task GetToken_CachesToken_AcrossMultipleCalls()
    {
        var loginCallCount = 0;
        var client = CreateClient(settings => settings.TVDB.ApiKey = "test-key", request =>
        {
            if (request.RequestUri?.AbsolutePath == "/login")
            {
                loginCallCount++;
                return LoginResponse("token");
            }
            return JsonResponse("""{"data":{"id":1,"name":"Test","type":"movie"}}""");
        });

        await client.GetMovie(1);
        await client.GetShow(2);

        Assert.Equal(1, loginCallCount);
    }

    [Fact]
    public async Task GetMovie_InvalidatesTokenAndRetries_On401Response()
    {
        var loginCallCount = 0;
        var movieCallCount = 0;
        var client = CreateClient(settings => settings.TVDB.ApiKey = "test-key", request =>
        {
            if (request.RequestUri?.AbsolutePath == "/login")
            {
                loginCallCount++;
                return LoginResponse($"token-{loginCallCount}");
            }
            movieCallCount++;
            return movieCallCount == 1
                ? new(HttpStatusCode.Unauthorized)
                : JsonResponse("""{"data":{"id":5,"name":"Dune","type":"movie"}}""");
        });

        var title = await client.GetMovie(5);

        Assert.NotNull(title);
        Assert.Equal(2, loginCallCount);
        Assert.Equal(2, movieCallCount);
    }

    [Fact]
    public async Task GetMovie_ThrowsHttpRequestException_AfterTwoUnauthorizedResponses()
    {
        var client = CreateClient(settings => settings.TVDB.ApiKey = "test-key", request =>
            request.RequestUri?.AbsolutePath switch
            {
                "/login" => LoginResponse("token"),
                _ => new(HttpStatusCode.Unauthorized),
            });

        await Assert.ThrowsAsync<HttpRequestException>(async () => await client.GetMovie(1));
    }

    [Fact]
    public async Task GetMovie_ThrowsInvalidOperationException_WhenApiKeyNotConfigured()
    {
        var client = CreateClient(settings => settings.TVDB.ApiKey = null, _ => new(HttpStatusCode.OK));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await client.GetMovie(1));
        Assert.Equal("TVDB API key is not configured.", ex.Message);
    }

    [Fact]
    public async Task GetToken_ErrorMessage_DoesNotContainApiKey()
    {
        const string apiKey = "secret-api-key-xyz";
        var client = CreateClient(settings => settings.TVDB.ApiKey = apiKey, request =>
            request.RequestUri?.AbsolutePath switch
            {
                "/login" => JsonResponse("""{"data":{}}"""), // token field absent
                _ => new(HttpStatusCode.OK),
            });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await client.GetMovie(1));
        Assert.DoesNotContain(apiKey, ex.Message);
    }

    [Fact]
    public async Task GetList_ReturnsEmpty_WhenDataPropertyMissing()
    {
        var client = CreateClient(settings => settings.TVDB.ApiKey = "test-key", request =>
            request.RequestUri?.AbsolutePath switch
            {
                "/login" => LoginResponse("token"),
                _ => JsonResponse("""{"status":"success"}"""),
            });

        var titles = await client.GetList(1, new TvdbBuilderQuery { Kind = MediaKind.Movie });

        Assert.Empty(titles);
    }

    [Fact]
    public async Task GetList_ParsesEntities_FromEntitiesArrayKey()
    {
        var client = CreateClient(settings => settings.TVDB.ApiKey = "test-key", request =>
            request.RequestUri?.AbsolutePath switch
            {
                "/login" => LoginResponse("token"),
                _ => JsonResponse("""{"data":{"entities":[{"id":10,"name":"Alien","type":"movie"}]}}"""),
            });

        var titles = await client.GetList(42, new TvdbBuilderQuery { Kind = MediaKind.Movie, Limit = 10 });

        var title = Assert.Single(titles);
        Assert.Equal("Alien", title.Title);
        Assert.Equal(MediaKind.Movie, title.Kind);
    }

    private static TvdbCollectionBuilderClient CreateClient(
        Action<ServerSettings> configure,
        Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var settings = new ServerSettings();
        configure(settings);

        var settingsProvider = new Mock<ISettingsProvider>();
        settingsProvider.Setup(p => p.GetSettings(It.IsAny<bool>())).Returns(settings);

        return new(settingsProvider.Object, new TestHttpClientFactory(new DelegateHttpMessageHandler(responder)));
    }

    private static HttpResponseMessage LoginResponse(string token) =>
        JsonResponse($"{{\"data\":{{\"token\":\"{token}\"}}}}");

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed class TestHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false)
        {
            BaseAddress = new Uri("http://tvdb.test/"),
        };
    }

    private sealed class DelegateHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }
}
