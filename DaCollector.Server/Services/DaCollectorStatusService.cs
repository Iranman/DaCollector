using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using DaCollector.Abstractions.MediaServers.Plex;
using DaCollector.Abstractions.Metadata.Enums;
using DaCollector.Server.Collections;
using DaCollector.Server.Plex;
using DaCollector.Server.Settings;

using Constants = DaCollector.Server.Server.Constants;

#nullable enable
namespace DaCollector.Server.Services;

/// <summary>
/// Builds a compact readiness view for the DaCollector collection-manager stack.
/// </summary>
public class DaCollectorStatusService(
    ISettingsProvider settingsProvider,
    PlexTargetService plexTargetService
)
{
    private const string MissingTmdbApiKey = "TMDB_API_KEY_GOES_HERE";

    public async Task<DaCollectorStatus> GetStatus(CancellationToken cancellationToken = default)
    {
        var settings = settingsProvider.GetSettings();
        return new()
        {
            Providers = GetProviderStatuses(settings),
            CollectionManager = GetCollectionManagerStatus(settings),
            PlexTarget = await GetPlexTargetStatus(settings, cancellationToken).ConfigureAwait(false),
        };
    }

    public IReadOnlyList<ProviderConnectionStatus> GetProviderStatuses() =>
        GetProviderStatuses(settingsProvider.GetSettings());

    public async Task<PlexTargetConnectionStatus> GetPlexTargetStatus(CancellationToken cancellationToken = default) =>
        await GetPlexTargetStatus(settingsProvider.GetSettings(), cancellationToken).ConfigureAwait(false);

    private static IReadOnlyList<ProviderConnectionStatus> GetProviderStatuses(IServerSettings settings) =>
    [
        GetTmdbStatus(settings),
        GetTvdbStatus(settings),
    ];

    private static ProviderConnectionStatus GetTmdbStatus(IServerSettings settings)
    {
        var hasUserApiKey = !string.IsNullOrWhiteSpace(settings.TMDB.UserApiKey);
        var hasBundledApiKey = !string.Equals(Constants.TMDB.ApiKey, MissingTmdbApiKey, StringComparison.Ordinal);
        var configured = hasUserApiKey || hasBundledApiKey;
        var warnings = new List<string>();
        if (!configured)
            warnings.Add("TMDB API key is not configured; live TMDB collection builders cannot fetch provider data.");

        return new()
        {
            Provider = ExternalProvider.TMDB,
            Name = "TMDB",
            Enabled = true,
            Configured = configured,
            Ready = configured,
            CredentialConfigured = hasUserApiKey,
            ConfigurationSource = hasUserApiKey ? "TMDB.UserApiKey" : hasBundledApiKey ? "Bundled TMDB API key" : null,
            CollectionBuilders = GetCollectionBuilders(ExternalProvider.TMDB),
            Warnings = warnings,
        };
    }

    private static ProviderConnectionStatus GetTvdbStatus(IServerSettings settings)
    {
        var hasApiKey = !string.IsNullOrWhiteSpace(settings.TVDB.ApiKey);
        var hasPin = !string.IsNullOrWhiteSpace(settings.TVDB.Pin);
        var warnings = new List<string>();
        if (!settings.TVDB.Enabled)
            warnings.Add("TVDB provider is disabled.");
        if (settings.TVDB.Enabled && !hasApiKey)
            warnings.Add("TVDB API key is not configured.");

        return new()
        {
            Provider = ExternalProvider.TVDB,
            Name = "TVDB",
            Enabled = settings.TVDB.Enabled,
            Configured = hasApiKey,
            Ready = settings.TVDB.Enabled && hasApiKey,
            CredentialConfigured = hasApiKey,
            SecondaryCredentialConfigured = hasPin,
            ConfigurationSource = hasApiKey ? "TVDB.ApiKey" : null,
            CacheExpirationDays = settings.TVDB.CacheExpirationDays,
            CollectionBuilders = GetCollectionBuilders(ExternalProvider.TVDB),
            Warnings = warnings,
        };
    }

    private static CollectionManagerConnectionStatus GetCollectionManagerStatus(IServerSettings settings)
    {
        var collections = settings.CollectionManager.Collections;
        return new()
        {
            ScheduledSyncEnabled = settings.CollectionManager.ScheduledSyncEnabled,
            SyncIntervalMinutes = settings.CollectionManager.SyncIntervalMinutes,
            CollectionCount = collections.Count,
            EnabledCollectionCount = collections.Count(collection => collection.Enabled),
            CollectionBuilderCount = CollectionBuilderCatalog.All.Count,
        };
    }

    private async Task<PlexTargetConnectionStatus> GetPlexTargetStatus(IServerSettings settings, CancellationToken cancellationToken)
    {
        var plexSettings = settings.Plex;
        var hasToken = !string.IsNullOrWhiteSpace(plexSettings.TargetToken);
        var hasSectionKey = !string.IsNullOrWhiteSpace(plexSettings.TargetSectionKey);
        var sectionKey = plexSettings.TargetSectionKey.Trim();
        var warnings = new List<string>();

        var identity = await plexTargetService.GetIdentity(cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!identity.Reachable)
            warnings.Add("Plex target identity endpoint is not reachable.");
        if (!hasToken)
            warnings.Add("Plex target token is not configured; libraries and collection sync cannot be updated.");
        if (!hasSectionKey)
            warnings.Add("Plex target section key is not configured; scheduled collection sync has no target library.");

        IReadOnlyList<PlexLibrarySection> libraries = [];
        string libraryStatus = hasToken ? "Not checked" : "Skipped because Plex target token is not configured.";
        var librariesReadable = false;
        if (hasToken)
        {
            try
            {
                libraries = await plexTargetService.GetLibraries(cancellationToken: cancellationToken).ConfigureAwait(false);
                libraryStatus = "OK";
                librariesReadable = true;
            }
            catch (Exception e) when (e is InvalidOperationException or HttpRequestException or TaskCanceledException or UriFormatException or XmlException)
            {
                libraryStatus = e.Message;
                warnings.Add("Plex libraries could not be read with the configured token.");
            }
        }

        var sectionFound = !hasSectionKey || !librariesReadable || libraries.Any(section => string.Equals(section.Key, sectionKey, StringComparison.OrdinalIgnoreCase));
        if (hasSectionKey && librariesReadable && !sectionFound)
            warnings.Add($"Configured Plex library section '{sectionKey}' was not found.");

        return new()
        {
            BaseUrl = identity.BaseUrl,
            SectionKey = hasSectionKey ? sectionKey : null,
            Configured = hasToken && hasSectionKey,
            Ready = identity.Reachable && hasToken && hasSectionKey && librariesReadable && sectionFound,
            TokenConfigured = hasToken,
            SectionKeyConfigured = hasSectionKey,
            Reachable = identity.Reachable,
            Identity = identity,
            LibraryStatus = libraryStatus,
            LibraryCount = librariesReadable ? libraries.Count : null,
            Libraries = libraries,
            Warnings = warnings,
        };
    }

    private static IReadOnlyList<string> GetCollectionBuilders(ExternalProvider provider) =>
        CollectionBuilderCatalog
            .All
            .Values
            .Where(builder => builder.Provider == provider)
            .Select(builder => builder.Name)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
}

public sealed record DaCollectorStatus
{
    public required IReadOnlyList<ProviderConnectionStatus> Providers { get; init; }

    public required CollectionManagerConnectionStatus CollectionManager { get; init; }

    public required PlexTargetConnectionStatus PlexTarget { get; init; }
}

public sealed record ProviderConnectionStatus
{
    public required ExternalProvider Provider { get; init; }

    public required string Name { get; init; }

    public required bool Enabled { get; init; }

    public required bool Configured { get; init; }

    public required bool Ready { get; init; }

    public bool CredentialConfigured { get; init; }

    public bool? SecondaryCredentialConfigured { get; init; }

    public string? ConfigurationSource { get; init; }

    public int? CacheExpirationDays { get; init; }

    public IReadOnlyList<string> CollectionBuilders { get; init; } = [];

    public IReadOnlyList<string> Warnings { get; init; } = [];
}

public sealed record CollectionManagerConnectionStatus
{
    public required bool ScheduledSyncEnabled { get; init; }

    public required int SyncIntervalMinutes { get; init; }

    public required int CollectionCount { get; init; }

    public required int EnabledCollectionCount { get; init; }

    public required int CollectionBuilderCount { get; init; }
}

public sealed record PlexTargetConnectionStatus
{
    public required string BaseUrl { get; init; }

    public string? SectionKey { get; init; }

    public required bool Configured { get; init; }

    public required bool Ready { get; init; }

    public required bool TokenConfigured { get; init; }

    public required bool SectionKeyConfigured { get; init; }

    public required bool Reachable { get; init; }

    public required PlexServerIdentity Identity { get; init; }

    public required string LibraryStatus { get; init; }

    public int? LibraryCount { get; init; }

    public IReadOnlyList<PlexLibrarySection> Libraries { get; init; } = [];

    public IReadOnlyList<string> Warnings { get; init; } = [];
}
