using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DaCollector.Abstractions.Collections;
using DaCollector.Abstractions.Metadata;
using DaCollector.Server.Plex;
using DaCollector.Server.Settings;
using Moq;
using Xunit;

#nullable enable
namespace DaCollector.Tests;

public class PlexTargetServiceTests
{
    [Fact]
    public async Task ApplyCollection_PreviewMode_ReturnsMatchesWithoutWriting()
    {
        var requests = new List<ObservedPlexRequest>();
        var service = CreateService(requests, request => request.RequestUri?.AbsolutePath switch
        {
            "/library/sections/1/all" => PlexLibraryItemsResponse(),
            _ => new(HttpStatusCode.NotFound),
        });

        var result = await service.ApplyCollection(
            "1",
            "Favorites",
            [new() { ExternalID = ExternalMediaId.TmdbMovie(11), Title = "Star Wars" }],
            CollectionSyncMode.Preview
        );

        Assert.False(result.Applied);
        Assert.Single(result.Match.Matched);
        Assert.Contains(@"D:\Media\Movies\Star Wars.mkv", result.Match.Matched[0].PlexItem.FilePaths);
        Assert.Empty(result.Match.Missing);
        Assert.Contains(result.Warnings, warning => warning.Contains("preview mode", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(requests, request => request.Method == HttpMethod.Put);
    }

    [Fact]
    public async Task ApplyCollection_AppendMode_AddsMatchedItemsToPlex()
    {
        var requests = new List<ObservedPlexRequest>();
        var service = CreateService(requests, request =>
        {
            if (request.Method == HttpMethod.Put)
                return XmlResponse("<MediaContainer />");

            return request.RequestUri?.AbsolutePath switch
            {
                "/library/sections/1/all" when request.RequestUri.Query.Contains("type=18", StringComparison.Ordinal) =>
                    XmlResponse("<MediaContainer totalSize=\"0\" />"),
                "/library/sections/1/all" => PlexLibraryItemsResponse(),
                _ => new(HttpStatusCode.NotFound),
            };
        });

        var result = await service.ApplyCollection(
            "1",
            "Favorites",
            [new() { ExternalID = ExternalMediaId.TmdbMovie(11), Title = "Star Wars" }],
            CollectionSyncMode.Append
        );

        var put = Assert.Single(requests.Where(request => request.Method == HttpMethod.Put));
        Assert.True(result.Applied);
        Assert.Equal(1, result.AddedItemCount);
        Assert.Equal(0, result.RemovedItemCount);
        Assert.Contains("id=100", put.Query);
        Assert.Contains("type=1", put.Query);
        Assert.Contains("collection", put.Query, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ApplyCollection_SyncMode_RemovesItemsNotInTargetSet()
    {
        var requests = new List<ObservedPlexRequest>();
        var service = CreateService(requests, request =>
        {
            if (request.Method == HttpMethod.Put)
                return XmlResponse("<MediaContainer />");

            return request.RequestUri?.AbsolutePath switch
            {
                "/library/sections/1/all" when request.RequestUri.Query.Contains("type=18", StringComparison.Ordinal) =>
                    XmlResponse("<MediaContainer totalSize=\"1\"><Directory ratingKey=\"999\" title=\"Favorites\" /></MediaContainer>"),
                "/library/sections/1/all" => TwoMovieLibraryResponse(),
                "/library/collections/999/children" => TwoMovieCollectionResponse(),
                _ => new(HttpStatusCode.NotFound),
            };
        });

        var result = await service.ApplyCollection(
            "1",
            "Favorites",
            [new() { ExternalID = ExternalMediaId.TmdbMovie(11), Title = "Star Wars" }],
            CollectionSyncMode.Sync
        );

        Assert.True(result.Applied);
        Assert.Equal(0, result.AddedItemCount);
        Assert.Equal(1, result.RemovedItemCount);
        var removePut = Assert.Single(requests.Where(request => request.Method == HttpMethod.Put));
        Assert.Contains("id=101", removePut.Query);
    }

    [Fact]
    public async Task ApplyCollection_DryRun_ComputesDiffWithoutWriting()
    {
        var requests = new List<ObservedPlexRequest>();
        var service = CreateService(requests, request =>
        {
            return request.RequestUri?.AbsolutePath switch
            {
                "/library/sections/1/all" when request.RequestUri.Query.Contains("type=18", StringComparison.Ordinal) =>
                    XmlResponse("<MediaContainer totalSize=\"0\" />"),
                "/library/sections/1/all" => PlexLibraryItemsResponse(),
                _ => new(HttpStatusCode.NotFound),
            };
        });

        var result = await service.ApplyCollection(
            "1",
            "Favorites",
            [new() { ExternalID = ExternalMediaId.TmdbMovie(11), Title = "Star Wars" }],
            CollectionSyncMode.Append,
            dryRun: true
        );

        Assert.False(result.Applied);
        Assert.True(result.DryRun);
        Assert.Equal(1, result.AddedItemCount);
        Assert.DoesNotContain(requests, request => request.Method == HttpMethod.Put);
    }

    [Fact]
    public async Task ApplyCollection_MissingProviderIDs_EmitsMatchWarnings()
    {
        var requests = new List<ObservedPlexRequest>();
        var service = CreateService(requests, request =>
        {
            return request.RequestUri?.AbsolutePath switch
            {
                "/library/sections/1/all" => LibraryWithUnidentifiedItemResponse(),
                _ => new(HttpStatusCode.NotFound),
            };
        });

        var result = await service.ApplyCollection(
            "1",
            "Favorites",
            [new() { ExternalID = ExternalMediaId.TmdbMovie(11), Title = "Star Wars" }],
            CollectionSyncMode.Preview
        );

        Assert.NotEmpty(result.Match.Warnings);
        Assert.Contains(result.Match.Warnings, w => w.Contains("no provider IDs", StringComparison.OrdinalIgnoreCase));
    }

    private static PlexTargetService CreateService(
        List<ObservedPlexRequest> requests,
        Func<HttpRequestMessage, HttpResponseMessage> responder
    )
    {
        var settings = new ServerSettings();
        settings.Plex.TargetBaseUrl = "http://plex.test:32400";
        settings.Plex.TargetToken = "plex-token";

        var settingsProvider = new Mock<ISettingsProvider>();
        settingsProvider.Setup(provider => provider.GetSettings(It.IsAny<bool>())).Returns(settings);

        return new(
            settingsProvider.Object,
            new TestHttpClientFactory(new DelegateHttpMessageHandler(request =>
            {
                requests.Add(new(request.Method, request.RequestUri?.AbsolutePath ?? string.Empty, request.RequestUri?.Query ?? string.Empty));
                return responder(request);
            }))
        );
    }

    private static HttpResponseMessage PlexLibraryItemsResponse() =>
        XmlResponse(
            "<MediaContainer totalSize=\"1\">" +
            "<Video ratingKey=\"100\" title=\"Star Wars\" type=\"movie\" year=\"1977\">" +
            "<Guid id=\"tmdb://11\" />" +
            "<Media><Part file=\"D:\\Media\\Movies\\Star Wars.mkv\" /></Media>" +
            "</Video>" +
            "</MediaContainer>"
        );

    private static HttpResponseMessage TwoMovieLibraryResponse() =>
        XmlResponse(
            "<MediaContainer totalSize=\"2\">" +
            "<Video ratingKey=\"100\" title=\"Star Wars\" type=\"movie\" year=\"1977\">" +
            "<Guid id=\"tmdb://11\" />" +
            "<Media><Part file=\"D:\\Media\\Movies\\Star Wars.mkv\" /></Media>" +
            "</Video>" +
            "<Video ratingKey=\"101\" title=\"The Empire Strikes Back\" type=\"movie\" year=\"1980\">" +
            "<Guid id=\"tmdb://1891\" />" +
            "<Media><Part file=\"D:\\Media\\Movies\\Empire.mkv\" /></Media>" +
            "</Video>" +
            "</MediaContainer>"
        );

    private static HttpResponseMessage TwoMovieCollectionResponse() =>
        XmlResponse(
            "<MediaContainer totalSize=\"2\">" +
            "<Video ratingKey=\"100\" title=\"Star Wars\" type=\"movie\" year=\"1977\">" +
            "<Guid id=\"tmdb://11\" />" +
            "</Video>" +
            "<Video ratingKey=\"101\" title=\"The Empire Strikes Back\" type=\"movie\" year=\"1980\">" +
            "<Guid id=\"tmdb://1891\" />" +
            "</Video>" +
            "</MediaContainer>"
        );

    private static HttpResponseMessage LibraryWithUnidentifiedItemResponse() =>
        XmlResponse(
            "<MediaContainer totalSize=\"2\">" +
            "<Video ratingKey=\"100\" title=\"Star Wars\" type=\"movie\" year=\"1977\">" +
            "<Guid id=\"tmdb://11\" />" +
            "<Media><Part file=\"D:\\Media\\Movies\\Star Wars.mkv\" /></Media>" +
            "</Video>" +
            "<Video ratingKey=\"200\" title=\"Unknown Movie\" type=\"movie\" year=\"2020\">" +
            "<Media><Part file=\"D:\\Media\\Movies\\Unknown.mkv\" /></Media>" +
            "</Video>" +
            "</MediaContainer>"
        );

    private static HttpResponseMessage XmlResponse(string xml) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(xml),
        };

    private sealed record ObservedPlexRequest(HttpMethod Method, string Path, string Query);

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
