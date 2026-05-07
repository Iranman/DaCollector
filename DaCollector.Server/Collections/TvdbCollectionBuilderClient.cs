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
using DaCollector.Abstractions.Metadata.Enums;
using DaCollector.Server.Settings;

#nullable enable
namespace DaCollector.Server.Collections;

public interface ITvdbCollectionBuilderClient
{
    Task<TvdbBuilderTitle?> GetMovie(int id, CancellationToken cancellationToken = default);

    Task<TvdbBuilderTitle?> GetShow(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TvdbBuilderTitle>> GetList(int id, TvdbBuilderQuery query, CancellationToken cancellationToken = default);
}

public class TvdbCollectionBuilderClient(ISettingsProvider settingsProvider, IHttpClientFactory httpClientFactory) : ITvdbCollectionBuilderClient
{
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private string? _cachedCredentialKey;
    private string? _cachedToken;
    private DateTimeOffset _cachedTokenExpiresAt;

    public async Task<TvdbBuilderTitle?> GetMovie(int id, CancellationToken cancellationToken = default)
    {
        var data = await GetData($"movies/{id}/extended?short=true", cancellationToken).ConfigureAwait(false);
        return data.HasValue && TryReadTitle(data.Value, MediaKind.Movie, out var title) ? title : null;
    }

    public async Task<TvdbBuilderTitle?> GetShow(int id, CancellationToken cancellationToken = default)
    {
        var data = await GetData($"series/{id}/extended?short=true", cancellationToken).ConfigureAwait(false);
        return data.HasValue && TryReadTitle(data.Value, MediaKind.Show, out var title) ? title : null;
    }

    public async Task<IReadOnlyList<TvdbBuilderTitle>> GetList(int id, TvdbBuilderQuery query, CancellationToken cancellationToken = default)
    {
        var data = await GetData($"lists/{id}/extended", cancellationToken).ConfigureAwait(false);
        if (!data.HasValue)
            return [];

        return EnumerateListEntities(data.Value)
            .Select(entity => TryReadTitle(entity, query.Kind, out var title) ? title : null)
            .Where(title => title is not null)
            .Select(title => title!)
            .Take(query.Limit)
            .ToList();
    }

    private async Task<JsonElement?> GetData(string relativePath, CancellationToken cancellationToken)
    {
        using var response = await SendAuthorized(HttpMethod.Get, relativePath, null, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode is HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        return TryGetProperty(document.RootElement, "data", out var data) ? data.Clone() : null;
    }

    private async Task<HttpResponseMessage> SendAuthorized(HttpMethod method, string relativePath, HttpContent? content, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var token = await GetToken(cancellationToken).ConfigureAwait(false);
            var client = httpClientFactory.CreateClient("TVDB");
            using var request = new HttpRequestMessage(method, relativePath)
            {
                Content = content,
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode is not HttpStatusCode.Unauthorized)
                return response;

            response.Dispose();
            InvalidateToken();
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
            var body = new Dictionary<string, string>
            {
                ["apikey"] = settings.ApiKey!.Trim(),
            };
            if (!string.IsNullOrWhiteSpace(settings.Pin))
                body["pin"] = settings.Pin.Trim();

            var client = httpClientFactory.CreateClient("TVDB");
            using var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            using var response = await client.PostAsync("login", content, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (!TryGetProperty(document.RootElement, "data", out var data) || !TryGetProperty(data, "token", out var tokenElement))
                throw new InvalidOperationException("TVDB login did not return a bearer token.");

            var token = tokenElement.GetString();
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

    private static IEnumerable<JsonElement> EnumerateListEntities(JsonElement data)
    {
        foreach (var key in new[] { "entities", "items", "records", "listItems" })
        {
            if (TryGetProperty(data, key, out var array) && array.ValueKind is JsonValueKind.Array)
            {
                foreach (var element in array.EnumerateArray())
                    yield return element;
                yield break;
            }
        }
    }

    private static bool TryReadTitle(JsonElement record, MediaKind requestedKind, out TvdbBuilderTitle title)
    {
        foreach (var nestedKey in new[] { "movie", "series", "show", "record" })
        {
            if (TryGetProperty(record, nestedKey, out var nested) && nested.ValueKind is JsonValueKind.Object)
            {
                var nestedKind = nestedKey is "movie" ? MediaKind.Movie : nestedKey is "series" or "show" ? MediaKind.Show : requestedKind;
                if (TryReadTitle(nested, nestedKind, out title))
                    return true;
            }
        }

        var actualKind = requestedKind is MediaKind.Unknown ? GetRecordKind(record) : requestedKind;
        if (actualKind is not (MediaKind.Movie or MediaKind.Show))
        {
            title = default!;
            return false;
        }

        var recordKind = GetRecordKind(record);
        if (recordKind is MediaKind.Movie or MediaKind.Show && recordKind != actualKind)
        {
            title = default!;
            return false;
        }

        if (!TryGetRecordId(record, actualKind, out var id))
        {
            title = default!;
            return false;
        }

        var name = FirstString(record, "name", "title", "name_translated")
            ?? $"TVDB {(actualKind is MediaKind.Movie ? "Movie" : "Show")} {id}";
        title = new(id, actualKind, name, BuildSummary(record));
        return true;
    }

    private static bool TryGetRecordId(JsonElement record, MediaKind kind, out int id)
    {
        var keys = kind is MediaKind.Movie
            ? new[] { "movieId", "movieID", "tvdb_id", "tvdbId", "id" }
            : new[] { "seriesId", "seriesID", "showId", "showID", "tvdb_id", "tvdbId", "id" };

        foreach (var key in keys)
        {
            if (!TryGetProperty(record, key, out var value))
                continue;
            if (TryReadPositiveInt(value, out id))
                return true;
        }

        id = 0;
        return false;
    }

    private static MediaKind GetRecordKind(JsonElement record)
    {
        var type = FirstString(record, "type", "recordType", "entityType", "kind");
        var normalized = type?.Trim().ToLowerInvariant() ?? string.Empty;
        if (normalized is "movie" or "movies")
            return MediaKind.Movie;
        if (normalized is "series" or "show" or "shows" or "tv" or "tvseries")
            return MediaKind.Show;

        if (TryGetProperty(record, "movieId", out _) || TryGetProperty(record, "movieID", out _))
            return MediaKind.Movie;
        if (TryGetProperty(record, "seriesId", out _) || TryGetProperty(record, "seriesID", out _))
            return MediaKind.Show;

        return MediaKind.Unknown;
    }

    private static string? BuildSummary(JsonElement record)
    {
        var parts = new List<string>();
        if (FirstString(record, "year", "firstAired", "released") is { Length: > 0 } year)
            parts.Add(year);
        if (FirstString(record, "overview", "overview_translated") is { Length: > 0 } overview)
            parts.Add(overview);

        return parts.Count == 0 ? null : string.Join(" | ", parts);
    }

    private static string? FirstString(JsonElement record, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!TryGetProperty(record, key, out var value))
                continue;

            if (value.ValueKind is JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()))
                return value.GetString();
            if (value.ValueKind is JsonValueKind.Array)
            {
                var firstString = value.EnumerateArray()
                    .Where(element => element.ValueKind is JsonValueKind.String)
                    .Select(element => element.GetString())
                    .FirstOrDefault(item => !string.IsNullOrWhiteSpace(item));
                if (!string.IsNullOrWhiteSpace(firstString))
                    return firstString;
            }
        }

        return null;
    }

    private static bool TryReadPositiveInt(JsonElement value, out int id)
    {
        if (value.ValueKind is JsonValueKind.Number && value.TryGetInt32(out id) && id > 0)
            return true;
        if (value.ValueKind is JsonValueKind.String && int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out id) && id > 0)
            return true;

        id = 0;
        return false;
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind is not JsonValueKind.Object)
        {
            value = default;
            return false;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}

public sealed record TvdbBuilderQuery
{
    public MediaKind Kind { get; init; } = MediaKind.Unknown;

    public int Limit { get; init; } = 20;
}

public sealed record TvdbBuilderTitle(int Id, MediaKind Kind, string Title, string? Summary);
