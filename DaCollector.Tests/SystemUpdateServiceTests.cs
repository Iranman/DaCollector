using System;
using DaCollector.Server.Services;
using Newtonsoft.Json;
using Xunit;

#nullable enable
namespace DaCollector.Tests;

public class SystemUpdateServiceTests
{
    [Fact]
    public void WebUIVersionInfo_InvalidDate_DoesNotThrow()
    {
        var info = JsonConvert.DeserializeObject<SystemUpdateService.WebUIVersionInfo>(
            """{"package":"0.0.1-local","minimumServerVersion":"0.0.1","tag":"v0.0.1-local","git":"local","date":"local","channel":"Stable"}""");

        Assert.NotNull(info);
        Assert.Null(info.Date);
    }

    [Fact]
    public void WebUIVersionInfo_ValidDate_RoundTripsAsIsoString()
    {
        var info = JsonConvert.DeserializeObject<SystemUpdateService.WebUIVersionInfo>(
            """{"package":"0.0.1-local","date":"2026-05-13T12:34:56Z","channel":"Stable"}""");

        Assert.NotNull(info);
        Assert.Equal(new DateTime(2026, 5, 13, 12, 34, 56, DateTimeKind.Utc), info.Date);

        var json = JsonConvert.SerializeObject(info);

        Assert.Contains("\"date\":\"2026-05-13T12:34:56.0000000Z\"", json);
    }

    [Fact]
    public void WebUIVersionInfo_LocalPrereleaseVersion_DoesNotThrow()
    {
        var info = JsonConvert.DeserializeObject<SystemUpdateService.WebUIVersionInfo>(
            """{"package":"0.0.1-local","minimumServerVersion":"0.0.1-dev.5","channel":"Stable"}""");

        Assert.NotNull(info);
        Assert.Equal(new Version(0, 0, 1), info.VersionAsVersion);
        Assert.Equal(new Version(0, 0, 1), info.MinimumServerVersion);
    }

    [Fact]
    public void WebUIVersionInfo_SinglePartLocalVersion_NormalizesToSemanticVersion()
    {
        var info = JsonConvert.DeserializeObject<SystemUpdateService.WebUIVersionInfo>(
            """{"package":"1-local","minimumServerVersion":"1.2+build","channel":"Stable"}""");

        Assert.NotNull(info);
        Assert.Equal(new Version(1, 0, 0), info.VersionAsVersion);
        Assert.Equal(new Version(1, 2, 0), info.MinimumServerVersion);
    }

    [Fact]
    public void WebUIVersionInfo_MalformedNonEmptyVersion_FallsBackToZeroVersion()
    {
        var info = JsonConvert.DeserializeObject<SystemUpdateService.WebUIVersionInfo>(
            """{"package":"local","minimumServerVersion":"not-a-version","channel":"Stable"}""");

        Assert.NotNull(info);
        Assert.Equal(new Version(0, 0, 0), info.VersionAsVersion);
        Assert.Equal(new Version(0, 0, 0), info.MinimumServerVersion);
    }
}
