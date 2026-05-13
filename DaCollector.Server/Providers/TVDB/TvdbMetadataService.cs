using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using DaCollector.Server.Models.TVDB;
using DaCollector.Server.Repositories.Cached.TVDB;
using DaCollector.Server.Settings;

#nullable enable
namespace DaCollector.Server.Providers.TVDB;

public class TvdbMetadataService(
    ILogger<TvdbMetadataService> logger,
    ISettingsProvider settingsProvider,
    IHttpClientFactory httpClientFactory,
    TVDB_ShowRepository tvdbShows,
    TVDB_MovieRepository tvdbMovies,
    TVDB_SeasonRepository tvdbSeasons,
    TVDB_EpisodeRepository tvdbEpisodes)
{
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private string? _cachedCredentialKey;
    private string? _cachedToken;
    private DateTimeOffset _cachedTokenExpiresAt;

    public async Task<TVDB_Show?> UpdateShow(int tvdbShowId, CancellationToken cancellationToken = default)
    {
        var data = await GetData($"series/{tvdbShowId}/extended", cancellationToken).ConfigureAwait(false);
        if (data is null)
        {
            logger.LogWarning("TVDB show {TvdbShowID} not found.", tvdbShowId);
            return null;
        }

        var show = tvdbShows.GetByTvdbShowID(tvdbShowId) ?? new TVDB_Show(tvdbShowId);
        var isNew = show.TVDB_ShowID == 0;
        show.Populate(data.Value);

        if (isNew)
            tvdbShows.Save(show);
        else
            tvdbShows.Save(show);

        UpdateSeasons(data.Value, tvdbShowId);
        UpdateEpisodes(data.Value, tvdbShowId);

        logger.LogInformation("Updated TVDB show {TvdbShowID} '{Name}'.", tvdbShowId, show.Name);
        return show;
    }

    public async Task<TVDB_Movie?> UpdateMovie(int tvdbMovieId, CancellationToken cancellationToken = default)
    {
        var data = await GetData($"movies/{tvdbMovieId}/extended", cancellationToken).ConfigureAwait(false);
        if (data is null)
        {
            logger.LogWarning("TVDB movie {TvdbMovieID} not found.", tvdbMovieId);
            return null;
        }

        var movie = tvdbMovies.GetByTvdbMovieID(tvdbMovieId) ?? new TVDB_Movie(tvdbMovieId);
        movie.Populate(data.Value);
        tvdbMovies.Save(movie);

        logger.LogInformation("Updated TVDB movie {TvdbMovieID} '{Name}'.", tvdbMovieId, movie.Name);
        return movie;
    }

    private void UpdateSeasons(JsonElement data, int tvdbShowId)
    {
        if (!data.TryGetProperty("seasons", out var seasonsEl) || seasonsEl.ValueKind is not JsonValueKind.Array)
            return;

        var episodesBySeason = new Dictionary<int, int>();
        if (data.TryGetProperty("episodes", out var episodesEl) && episodesEl.ValueKind is JsonValueKind.Array)
        {
            foreach (var ep in episodesEl.EnumerateArray())
            {
                if (ep.TryGetProperty("seasonNumber", out var snProp) && snProp.TryGetInt32(out var sn))
                    episodesBySeason[sn] = episodesBySeason.GetValueOrDefault(sn) + 1;
            }
        }

        foreach (var seasonEl in seasonsEl.EnumerateArray())
        {
            if (!seasonEl.TryGetProperty("id", out var idProp) || !idProp.TryGetInt32(out var seasonId))
                continue;

            var seasonNumber = seasonEl.TryGetProperty("number", out var numProp) && numProp.TryGetInt32(out var num) ? num : 0;
            var season = tvdbSeasons.GetByTvdbSeasonID(seasonId) ?? new TVDB_Season(seasonId, tvdbShowId);
            season.Populate(seasonEl, episodesBySeason.GetValueOrDefault(seasonNumber));
            tvdbSeasons.Save(season);
        }
    }

    private void UpdateEpisodes(JsonElement data, int tvdbShowId)
    {
        if (!data.TryGetProperty("episodes", out var episodesEl) || episodesEl.ValueKind is not JsonValueKind.Array)
            return;

        foreach (var epEl in episodesEl.EnumerateArray())
        {
            if (!epEl.TryGetProperty("id", out var idProp) || !idProp.TryGetInt32(out var episodeId))
                continue;

            var episode = tvdbEpisodes.GetByTvdbEpisodeID(episodeId) ?? new TVDB_Episode(episodeId, tvdbShowId);
            episode.Populate(epEl, tvdbShowId);

            if (epEl.TryGetProperty("seasonId", out var sidProp) && sidProp.TryGetInt32(out var sid))
                episode.TvdbSeasonID = sid;

            tvdbEpisodes.Save(episode);
        }
    }

    private async Task<JsonElement?> GetData(string relativePath, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var token = await GetToken(cancellationToken).ConfigureAwait(false);
            var client = httpClientFactory.CreateClient("TVDB");
            using var request = new HttpRequestMessage(HttpMethod.Get, relativePath);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode is HttpStatusCode.NotFound)
                return null;

            if (response.StatusCode is HttpStatusCode.Unauthorized)
            {
                InvalidateToken();
                continue;
            }

            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (document.RootElement.TryGetProperty("data", out var data) && data.ValueKind is not JsonValueKind.Null)
                return data.Clone();
            return null;
        }

        throw new HttpRequestException("TVDB request was unauthorized after refreshing the bearer token.");
    }

    private async Task<string> GetToken(CancellationToken cancellationToken)
    {
        var credentialKey = GetCredentialKey();
        if (IsTokenUsable(credentialKey))
            return _cachedToken!;

        await _tokenLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (IsTokenUsable(credentialKey))
                return _cachedToken!;

            var settings = settingsProvider.GetSettings().TVDB;
            var body = new Dictionary<string, string> { ["apikey"] = settings.ApiKey!.Trim() };
            if (!string.IsNullOrWhiteSpace(settings.Pin))
                body["pin"] = settings.Pin.Trim();

            var client = httpClientFactory.CreateClient("TVDB");
            using var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            using var response = await client.PostAsync("login", content, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (!document.RootElement.TryGetProperty("data", out var data) || !data.TryGetProperty("token", out var tokenEl))
                throw new InvalidOperationException("TVDB login did not return a bearer token.");

            var token = tokenEl.GetString();
            if (string.IsNullOrWhiteSpace(token))
                throw new InvalidOperationException("TVDB login returned an empty bearer token.");

            _cachedCredentialKey = credentialKey;
            _cachedToken = token;
            _cachedTokenExpiresAt = DateTimeOffset.UtcNow.AddDays(29);
            return token;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private string GetCredentialKey()
    {
        var settings = settingsProvider.GetSettings().TVDB;
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
            throw new InvalidOperationException("TVDB API key is not configured.");
        return $"{settings.ApiKey.Trim()}\n{settings.Pin?.Trim()}";
    }

    private bool IsTokenUsable(string credentialKey) =>
        !string.IsNullOrWhiteSpace(_cachedToken)
            && DateTimeOffset.UtcNow < _cachedTokenExpiresAt
            && string.Equals(_cachedCredentialKey, credentialKey, StringComparison.Ordinal);

    private void InvalidateToken()
    {
        _cachedToken = null;
        _cachedTokenExpiresAt = DateTimeOffset.MinValue;
    }

    public async Task<(bool Success, string? Error)> TestCredentials(string apiKey, string? pin, CancellationToken cancellationToken = default)
    {
        try
        {
            var body = new Dictionary<string, string> { ["apikey"] = apiKey.Trim() };
            if (!string.IsNullOrWhiteSpace(pin))
                body["pin"] = pin.Trim();

            var client = httpClientFactory.CreateClient("TVDB");
            using var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            using var response = await client.PostAsync("login", content, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return (false, $"TVDB returned {(int)response.StatusCode}: {response.ReasonPhrase}");

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (!document.RootElement.TryGetProperty("data", out var data) || !data.TryGetProperty("token", out var tokenEl) || string.IsNullOrWhiteSpace(tokenEl.GetString()))
                return (false, "TVDB login did not return a bearer token.");

            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
}
