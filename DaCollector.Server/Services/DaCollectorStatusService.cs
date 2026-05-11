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
            ServerCapabilities = GetServerCapabilities(),
        };
    }

    public IReadOnlyList<ProviderConnectionStatus> GetProviderStatuses() =>
        GetProviderStatuses(settingsProvider.GetSettings());

    public IReadOnlyList<ServerCapabilityStatus> GetServerCapabilities() =>
    [
        new()
        {
            Key = "scan-folders",
            Name = "Scan folders",
            Completed = true,
            Status = "Complete",
            Summary = "Managed folders can be added, watched, notified, and scanned through the server.",
            Components =
            [
                "ManagedFolderController",
                "VideoService",
                "Scanner",
                "FileWatcherService",
                "ScanFolderJob",
                "ScanDropFoldersJob",
            ],
            ApiRoutes =
            [
                "GET /api/v3/ManagedFolder",
                "POST /api/v3/ManagedFolder",
                "GET /api/v3/ManagedFolder/{folderID}/Scan",
                "POST /api/v3/ManagedFolder/{folderID}/NotifyChangeDetected",
            ],
        },
        new()
        {
            Key = "hash-files",
            Name = "Hash files",
            Completed = true,
            Status = "Complete",
            Summary = "Files are fingerprinted by the hashing service and persisted as local video/hash records.",
            Components =
            [
                "VideoHashingService",
                "HashFileJob",
                "VideoLocal",
                "VideoLocal_HashDigest",
            ],
            ApiRoutes =
            [
                "GET /api/v3/File/Hash/ED2K",
                "GET /api/v3/File/Hash/CRC32",
                "GET /api/v3/File/Hash/MD5",
                "GET /api/v3/File/Hash/SHA1",
            ],
        },
        new()
        {
            Key = "parse-filenames",
            Name = "Parse filenames",
            Completed = true,
            Status = "Complete",
            Summary = "Movie and TV filenames can be parsed into first-pass media guesses before provider matching.",
            Components =
            [
                "FilenameParserService",
                "ParserController",
                "MediaFileReviewService",
            ],
            ApiRoutes =
            [
                "GET /api/v3/Parser/Filename",
                "POST /api/v3/Parser/Filename",
                "POST /api/v3/MediaFileReview/Files/{fileID}/RefreshParse",
            ],
            Notes =
            [
                "Scanned-file parser guesses are persisted in MediaFileReviewState for unmatched-review workflows.",
            ],
        },
        new()
        {
            Key = "match-files",
            Name = "Match files",
            Completed = true,
            Status = "Complete",
            Summary = "Provider match candidates and unmatched local files can be generated, reviewed, approved, ignored, or manually matched.",
            Components =
            [
                "ProviderMatchQueueService",
                "ProviderMatchController",
                "ProviderMatchCandidate",
                "MediaFileReviewService",
                "MediaFileMatchCandidateService",
                "MediaFileReviewController",
                "MediaFileReviewState",
                "MediaFileMatchCandidate",
                "TmdbSearchService",
                "TmdbMetadataService",
                "TvdbMetadataService",
                "TMDB provider cache",
                "TVDB provider cache",
            ],
            ApiRoutes =
            [
                "GET /api/v3/MediaFileReview/Files/Unmatched",
                "GET /api/v3/MediaFileReview/Files/{fileID}",
                "POST /api/v3/MediaFileReview/Files/{fileID}/ScanMatches",
                "POST /api/v3/MediaFileReview/Files/ScanMatches",
                "GET /api/v3/MediaFileReview/Files/{fileID}/Candidates",
                "GET /api/v3/MediaFileReview/Candidates",
                "POST /api/v3/MediaFileReview/Candidates/{candidateID}/Approve",
                "DELETE /api/v3/MediaFileReview/Candidates/{candidateID}",
                "POST /api/v3/MediaFileReview/Files/{fileID}/Ignore",
                "POST /api/v3/MediaFileReview/Files/{fileID}/Unignore",
                "POST /api/v3/MediaFileReview/Files/{fileID}/ManualMatch",
                "DELETE /api/v3/MediaFileReview/Files/{fileID}/ManualMatch",
                "POST /api/v3/ProviderMatch/Scan",
                "POST /api/v3/ProviderMatch/Series/{mediaSeriesID}/Scan",
                "GET /api/v3/ProviderMatch/Candidates",
                "POST /api/v3/ProviderMatch/Candidates/{candidateID}/Approve",
                "DELETE /api/v3/ProviderMatch/Candidates/{candidateID}",
            ],
            Notes =
            [
                "Cached TMDB/TVDB matching is the default. File candidate scans can optionally perform online TMDB search and refresh explicit TMDB/TVDB IDs.",
                "TVDB title search is not yet exposed; TVDB online refresh currently works for explicit TVDB IDs.",
            ],
        },
        new()
        {
            Key = "fetch-metadata",
            Name = "Fetch metadata",
            Completed = true,
            Status = "Complete",
            Summary = "TMDB and TVDB metadata refresh paths populate provider caches used by media and collection workflows.",
            Components =
            [
                "TmdbMetadataService",
                "TmdbController",
                "TvdbController",
                "TvdbCollectionBuilderClient",
            ],
            ApiRoutes =
            [
                "POST /api/v3/Tmdb/Movie/{movieID}/Action/Refresh",
                "POST /api/v3/Tmdb/Show/{showID}/Action/Refresh",
                "POST /api/v3/Tvdb/Movie/{tvdbMovieID}/Refresh",
                "POST /api/v3/Tvdb/Show/{tvdbShowID}/Refresh",
            ],
        },
        new()
        {
            Key = "store-database-records",
            Name = "Store database records",
            Completed = true,
            Status = "Complete",
            Summary = "NHibernate repositories, mappings, and SQLite migrations store local file, folder, metadata, match, user, and job state.",
            Components =
            [
                "SQLite migrations",
                "NHibernate mappings",
                "RepoFactory",
                "VideoLocal",
                "VideoLocal_Place",
                "ProviderMatchCandidate",
                "MediaSeries_User",
                "VideoLocal_User",
            ],
        },
        new()
        {
            Key = "track-watched-status",
            Name = "Track watched status",
            Completed = true,
            Status = "Complete",
            Summary = "Watched state can be read and changed for files, episodes, and series-level episode sets.",
            Components =
            [
                "UserDataService",
                "FileController",
                "EpisodeController",
                "SeriesController",
                "VideoLocal_User",
                "MediaEpisode_User",
                "MediaSeries_User",
            ],
            ApiRoutes =
            [
                "POST /api/v3/File/{fileID}/Watched/{watched?}",
                "POST /api/v3/Episode/{episodeID}/Watched/{watched}",
                "POST /api/v3/Series/{seriesID}/Episode/Watched",
            ],
            Notes =
            [
                "Plex watched sync remains an adapter/relay integration concern, with DaCollector Server retaining the authoritative local watch state.",
            ],
        },
        new()
        {
            Key = "expose-api-endpoints",
            Name = "Expose API endpoints",
            Completed = true,
            Status = "Complete",
            Summary = "ASP.NET Core controllers expose authenticated v3 APIs plus inherited compatibility endpoints.",
            Components =
            [
                "API/v3 controllers",
                "MediaController",
                "MediaReadService",
                "Swagger",
                "API versioning",
                "Authentication",
            ],
            ApiRoutes =
            [
                "GET /api/v3/DaCollectorStatus",
                "GET /api/v3/Dashboard",
                "GET /api/v3/Media/Movies",
                "GET /api/v3/Media/Shows",
                "GET /api/v3/Media/Shows/{provider}/{providerID}/Seasons",
                "GET /api/v3/Media/Shows/{provider}/{providerID}/Episodes",
                "GET /api/v3/Media/Files",
                "GET /api/v3/File",
                "GET /api/v3/ManagedFolder",
                "GET /api/v3/Plugin",
            ],
        },
        new()
        {
            Key = "serve-web-ui",
            Name = "Serve the Web UI",
            Completed = true,
            Status = "Complete",
            Summary = "The server can serve bundled WebUI assets and the combined Docker image builds the WebUI into the server runtime.",
            Components =
            [
                "WebUIController",
                "WebUIFileProvider",
                "Dockerfile.combined",
            ],
            ApiRoutes =
            [
                "GET /webui",
                "GET /api/v3/WebUI",
            ],
        },
        new()
        {
            Key = "talk-to-plugins",
            Name = "Talk to plugins",
            Completed = true,
            Status = "Complete",
            Summary = "Plugin discovery, package management, API version discovery, and extension points are wired into the server.",
            Components =
            [
                "PluginManager",
                "PluginPackageManager",
                "PluginController",
                "PluginPackageController",
                "IManagedFolderIgnoreRule",
            ],
            ApiRoutes =
            [
                "GET /api/v3/Plugin",
                "GET /api/v3/Plugin/Package",
            ],
        },
        new()
        {
            Key = "run-background-jobs",
            Name = "Run background jobs",
            Completed = true,
            Status = "Complete",
            Summary = "Quartz schedules recurring and ad-hoc server jobs for scanning, hashing, metadata, collections, and maintenance.",
            Components =
            [
                "QuartzStartup",
                "ScanFolderJob",
                "HashFileJob",
                "SyncManagedCollectionsJob",
                "QueueController",
            ],
            ApiRoutes =
            [
                "GET /api/v3/Queue",
                "POST /api/v3/Action/SyncHashes",
            ],
        },
    ];

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

    public required IReadOnlyList<ServerCapabilityStatus> ServerCapabilities { get; init; }
}

public sealed record ServerCapabilityStatus
{
    public required string Key { get; init; }

    public required string Name { get; init; }

    public required bool Completed { get; init; }

    public required string Status { get; init; }

    public required string Summary { get; init; }

    public IReadOnlyList<string> Components { get; init; } = [];

    public IReadOnlyList<string> ApiRoutes { get; init; } = [];

    public IReadOnlyList<string> Notes { get; init; } = [];
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
