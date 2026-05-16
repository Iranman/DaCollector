using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using DaCollector.Server.Settings;

#nullable enable
namespace DaCollector.Server.Integrations;

public class RadarrService(ISettingsProvider settingsProvider, IHttpClientFactory httpClientFactory, ILogger<RadarrService> logger)
{
    public record QualityProfile(int Id, string Name);
    public record RootFolder(int Id, string Path);
    public record MovieLookup(int TmdbId, string Title, int? Year, bool IsAvailable);
    public record RequestResult(bool Success, string? Error);

    public async Task<string> TestConnection(CancellationToken cancellationToken = default)
    {
        var (baseUrl, apiKey) = GetConfig();
        using var client = CreateClient(apiKey);
        using var response = await client.GetAsync($"{baseUrl}/api/v3/system/status", cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            return $"Radarr returned {(int)response.StatusCode}";
        var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var obj = JObject.Parse(content);
        return $"Connected · Radarr {obj["version"]?.Value<string>() ?? "unknown"}";
    }

    public async Task<IReadOnlyList<QualityProfile>> GetQualityProfiles(CancellationToken cancellationToken = default)
    {
        var (baseUrl, apiKey) = GetConfig();
        using var client = CreateClient(apiKey);
        using var response = await client.GetAsync($"{baseUrl}/api/v3/qualityprofile", cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var array = JArray.Parse(content);
        var result = new List<QualityProfile>();
        foreach (var item in array)
            result.Add(new(item["id"]!.Value<int>(), item["name"]!.Value<string>()!));
        return result;
    }

    public async Task<IReadOnlyList<RootFolder>> GetRootFolders(CancellationToken cancellationToken = default)
    {
        var (baseUrl, apiKey) = GetConfig();
        using var client = CreateClient(apiKey);
        using var response = await client.GetAsync($"{baseUrl}/api/v3/rootfolder", cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var array = JArray.Parse(content);
        var result = new List<RootFolder>();
        foreach (var item in array)
            result.Add(new(item["id"]!.Value<int>(), item["path"]!.Value<string>()!));
        return result;
    }

    public async Task<MovieLookup?> LookupByTmdbId(int tmdbId, CancellationToken cancellationToken = default)
    {
        var (baseUrl, apiKey) = GetConfig();
        using var client = CreateClient(apiKey);
        using var response = await client.GetAsync($"{baseUrl}/api/v3/movie?tmdbId={tmdbId}", cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return null;
        var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var array = JArray.Parse(content);
        if (array.Count == 0) return null;
        var item = array[0];
        return new(
            item["tmdbId"]?.Value<int>() ?? tmdbId,
            item["title"]?.Value<string>() ?? string.Empty,
            item["year"]?.Value<int>(),
            item["hasFile"]?.Value<bool>() ?? false
        );
    }

    public async Task<RequestResult> RequestMovie(int tmdbId, CancellationToken cancellationToken = default)
    {
        var settings = settingsProvider.GetSettings().Radarr;
        if (!settings.Enabled || string.IsNullOrWhiteSpace(settings.BaseUrl) || string.IsNullOrWhiteSpace(settings.ApiKey))
            return new(false, "Radarr is not configured.");

        var lookup = await LookupByTmdbId(tmdbId, cancellationToken).ConfigureAwait(false);
        if (lookup == null)
            return new(false, $"TMDB ID {tmdbId} not found in Radarr.");

        var (baseUrl, apiKey) = GetConfig();
        using var client = CreateClient(apiKey);

        var payload = new JObject
        {
            ["tmdbId"] = tmdbId,
            ["title"] = lookup.Title,
            ["qualityProfileId"] = settings.QualityProfileId,
            ["rootFolderPath"] = settings.RootFolderPath,
            ["monitored"] = true,
            ["addOptions"] = new JObject { ["searchForMovie"] = true },
        };

        using var content = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");
        using var response = await client.PostAsync($"{baseUrl}/api/v3/movie", content, cancellationToken).ConfigureAwait(false);
        if (response.IsSuccessStatusCode)
        {
            logger.LogInformation("Requested movie TMDB:{TmdbId} from Radarr", tmdbId);
            return new(true, null);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        logger.LogWarning("Radarr returned {Status} for TMDB:{TmdbId}: {Body}", (int)response.StatusCode, tmdbId, body);
        return new(false, $"Radarr returned {(int)response.StatusCode}");
    }

    private (string baseUrl, string apiKey) GetConfig()
    {
        var s = settingsProvider.GetSettings().Radarr;
        var baseUrl = s.BaseUrl.TrimEnd('/');
        return (baseUrl, s.ApiKey);
    }

    private HttpClient CreateClient(string apiKey)
    {
        var client = httpClientFactory.CreateClient("Radarr");
        client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        return client;
    }
}
