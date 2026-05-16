using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using DaCollector.Abstractions.Connectivity.Services;
using DaCollector.Abstractions.Core;
using DaCollector.Abstractions.Core.Services;
using DaCollector.Abstractions.Extensions;
using DaCollector.Abstractions.User.Services;
using DaCollector.Abstractions.Utilities;
using DaCollector.Abstractions.Web.Attributes;
using DaCollector.Server.API.Annotations;
using DaCollector.Server.API.v3.Models.Common;
using DaCollector.Server.Databases;
using DaCollector.Server.MediaInfo;
using DaCollector.Server.Providers.TMDB;
using DaCollector.Server.Providers.TVDB;
using DaCollector.Server.Repositories;
using DaCollector.Server.Services;
using DaCollector.Server.Settings;

using Constants = DaCollector.Server.Server.Constants;
using ServerStatus = DaCollector.Server.API.v3.Models.DaCollector.ServerStatus;

#nullable enable
namespace DaCollector.Server.API.v3.Controllers;

// ReSharper disable once UnusedMember.Global
/// <summary>
/// The init controller. Use this for first time setup. Settings will also allow full control to the init user.
/// </summary>
[ApiController, Route("/api/v{version:apiVersion}/[controller]"), ApiV3]
[DatabaseBlockedExempt]
[InitFriendly]
public class InitController : BaseController
{
    private readonly ILogger<InitController> _logger;
    private readonly SystemService _systemService;
    private readonly IConnectivityService _connectivityService;
    private readonly ISystemUpdateService _webUIUpdateService;
    private readonly TmdbMetadataService _tmdbService;
    private readonly TvdbMetadataService _tvdbService;
    private readonly IUserService _userService;

    public InitController(
        ILogger<InitController> logger,
        SystemService systemService,
        ISettingsProvider settingsProvider,
        IConnectivityService connectivityService,
        ISystemUpdateService webUIUpdateService,
        TmdbMetadataService tmdbService,
        TvdbMetadataService tvdbService,
        IUserService userService
    ) : base(settingsProvider)
    {
        _logger = logger;
        _systemService = systemService;
        _connectivityService = connectivityService;
        _webUIUpdateService = webUIUpdateService;
        _tmdbService = tmdbService;
        _tvdbService = tvdbService;
        _userService = userService;
    }

    /// <summary>
    /// Return current version of DaCollector and several modules
    /// This will work after init
    /// </summary>
    /// <returns></returns>
    [HttpGet("Version")]
    public ComponentVersionSet GetVersion()
    {
        var versionSet = new ComponentVersionSet()
        {
            Server = new()
            {
                Version = _systemService.Version.Version.ToSemanticVersioningString(),
                ReleaseChannel = Enum.Parse<ReleaseChannel>(_systemService.Version.Channel.ToString()),
                Commit = _systemService.Version.SourceRevision,
                Tag = _systemService.Version.ReleaseTag,
                ReleaseDate = _systemService.Version.ReleasedAt,
            },
        };

        try
        {
            if (MediaInfoUtility.GetVersion() is { Length: > 0 } mediaInfoVersion)
                versionSet.MediaInfo = new() { Version = mediaInfoVersion };
        }
        catch { }

        var webuiVersion = _webUIUpdateService.LoadWebComponentVersionInformation();
        if (webuiVersion != null)
        {
            versionSet.WebUI = new()
            {
                Version = webuiVersion.Version.ToSemanticVersioningString(),
                MinimumServerVersion = webuiVersion.MinimumServerVersion?.ToSemanticVersioningString(),
                Tag = webuiVersion.ReleaseTag,
                ReleaseChannel = webuiVersion.Channel,
                Commit = webuiVersion.SourceRevision,
                ReleaseDate = webuiVersion.ReleasedAt,
            };
        }

        return versionSet;
    }

    /// <summary>
    /// Gets various information about the startup status of the server
    /// This will work after init
    /// </summary>
    /// <remarks>
    /// To get the uptime, database blocked, etc. you need to authenticate when
    /// not in setup mode or a failed startup.
    /// </remarks>
    /// <returns></returns>
    [HttpGet("Status")]
    public ServerStatus GetServerStatus()
    {
        var isLoggedIn = User is not null;
        var message = (string)null;
        var state = ServerStatus.StartupState.Waiting;
        if (_systemService.IsStarted)
        {
            state = ServerStatus.StartupState.Started;
        }
        else if (_systemService.StartupFailedException is { } ex)
        {
            message = ex.Message;
            state = ServerStatus.StartupState.Failed;
        }
        else if (!_systemService.InSetupMode)
        {
            message = _systemService.StartupMessage;
            if (message.Equals("Complete!")) message = null;
            state = ServerStatus.StartupState.Starting;
        }
        if (!isLoggedIn)
            return new()
            {
                State = state,
                StartupMessage = message,
            };
        return new()
        {
            State = state,
            StartupMessage = message,
            BootstrappedAt = _systemService.BootstrappedAt.ToUniversalTime(),
            StartedAt = _systemService.StartedAt?.ToUniversalTime(),
            Uptime = _systemService.Uptime,
            StartupTime = _systemService.StartupTime ?? TimeSpan.Zero,
            CanShutdown = _systemService.CanShutdown,
            CanRestart = _systemService.CanRestart,
            DatabaseBlocked = new()
            {
                Blocked = _systemService.IsDatabaseBlocked,
                Reason = string.Empty,
            },
        };
    }

    /// <summary>
    /// Gets the current network connectivity details for the server.
    /// </summary>
    /// <returns></returns>
    [HttpGet("Connectivity")]
    public ActionResult<ConnectivityDetails> GetNetworkAvailability()
    {
        return new ConnectivityDetails
        {
            NetworkAvailability = _connectivityService.NetworkAvailability,
            LastChangedAt = _connectivityService.LastChangedAt,
        };
    }

    /// <summary>
    /// Forcefully re-checks the current network connectivity, then returns the
    /// updated details for the server.
    /// </summary>
    /// <returns></returns>
    [Authorize(Roles = "admin,init")]
    [HttpPost("Connectivity")]
    public async Task<ActionResult<ConnectivityDetails>> CheckNetworkAvailability()
    {
        await _connectivityService.CheckAvailability();

        return GetNetworkAvailability();
    }

    /// <summary>
    /// Gets whether anything is actively using the API
    /// </summary>
    /// <returns></returns>
    [HttpGet("InUse")]
    public bool ApiInUse() => ApiInUseAttribute.IsInUse;

    /// <summary>
    /// Gets the Default user's credentials. Will only return on first run
    /// </summary>
    /// <returns></returns>
    [Authorize("init")]
    [HttpGet("DefaultUser")]
    public ActionResult<Credentials> GetDefaultUserCredentials()
    {
        var settings = SettingsProvider.GetSettings();
        return new Credentials
        {
            Username = settings.Database.DefaultUserUsername,
            Password = settings.Database.DefaultUserPassword
        };
    }

    /// <summary>
    /// Sets the default user's credentials
    /// </summary>
    /// <returns></returns>
    [Authorize("init")]
    [HttpPost("DefaultUser")]
    public ActionResult SetDefaultUserCredentials(Credentials credentials)
    {
        try
        {
            var settings = SettingsProvider.GetSettings();
            settings.Database.DefaultUserUsername = credentials.Username;
            settings.Database.DefaultUserPassword = credentials.Password;
            return Ok();
        }
        catch
        {
            return InternalError();
        }
    }

    /// <summary>
    /// Starts the server unless it's already running.
    /// </summary>
    /// <returns></returns>
    [Obsolete("This is now deprecated, please use CompleteSetup instead.")]
    [Authorize("init")]
    [HttpGet("StartServer")]
    public ActionResult StartServer()
        => CompleteSetup();

    /// <summary>
    /// Tells the server that the setup process is complete and it can start
    /// now.
    /// </summary>
    /// <returns></returns>
    [Authorize("init")]
    [HttpPost("CompleteSetup")]
    public ActionResult CompleteSetup()
    {
        if (_systemService.IsStarted)
            return BadRequest("Already Running");
        if (_systemService.StartupFailedException is not null)
            return BadRequest("Startup Failed");
        if (!_systemService.InSetupMode)
            return BadRequest("Already Starting");
        try
        {
            _systemService.CompleteSetup();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "There was an error starting the server");
            return InternalError($"There was an error starting the server: {e}");
        }
        return Ok();
    }

    /// <summary>
    /// Requests the server to shutdown.
    /// </summary>
    /// <returns></returns>
    [Authorize(Roles = "admin,init")]
    [HttpPost("Shutdown")]
    public ActionResult StopServer()
    {
        if (!_systemService.CanShutdown)
            return BadRequest("Shutdown is disabled for this instance");
        if (_systemService.ShutdownPending)
            return BadRequest("Shutdown already requested");
        if (!_systemService.RequestShutdown())
            return BadRequest("Shutdown request blocked");
        return Ok("Shutdown Requested");
    }

    /// <summary>
    /// Requests the server to restart if possible.
    /// </summary>
    /// <returns></returns>
    [Authorize(Roles = "admin,init")]
    [HttpPost("Restart")]
    public ActionResult RestartServer()
    {
        if (!_systemService.CanRestart)
            return BadRequest("Restart is disabled for this instance");
        if (_systemService.ShutdownPending)
            return BadRequest("Shutdown already requested");
        if (!_systemService.RequestRestart())
            return BadRequest("Restart request blocked");
        return Ok("Restart Requested");
    }

    /// <summary>
    /// Resets the server to setup mode and restarts it.
    /// </summary>
    [Authorize(Roles = "admin")]
    [HttpPost("ResetSetup")]
    public ActionResult ResetSetup()
    {
        if (!_systemService.CanRestart)
            return BadRequest("Restart is not available for this instance.");
        if (_systemService.ShutdownPending)
            return BadRequest("Shutdown already requested.");
        var settings = SettingsProvider.GetSettings();
        settings.FirstRun = true;
        SettingsProvider.SaveSettings(settings);
        if (!_systemService.RequestRestart())
            return BadRequest("Restart request blocked.");
        return Ok();
    }

    /// <summary>
    /// Test Database Connection with Current Settings
    /// </summary>
    /// <returns>200 if connection successful, 400 otherwise</returns>
    [Authorize("init")]
    [HttpGet("Database/Test")]
    public ActionResult TestDatabaseConnection()
    {
        var settings = SettingsProvider.GetSettings();
        return settings.Database.Type switch
        {
            Constants.DatabaseType.MySQL when new MySQL(_systemService).TestConnection() => Ok(),
            Constants.DatabaseType.SQLServer when new SQLServer(_systemService).TestConnection() => Ok(),
            Constants.DatabaseType.SQLite when new SQLite(_systemService).TestConnection() => Ok(),
            _ => BadRequest("Failed to Connect")
        };
    }

    /// <summary>
    /// Gets the current TMDB provider setup state.
    /// </summary>
    [Authorize("init")]
    [HttpGet("Provider/TMDB")]
    public ActionResult<ProviderSetupInfo> GetTmdbProvider()
    {
        var tmdb = SettingsProvider.GetSettings().TMDB;
        return new ProviderSetupInfo { Configured = !string.IsNullOrWhiteSpace(tmdb.UserApiKey) };
    }

    /// <summary>
    /// Saves the TMDB API key and tests connectivity.
    /// </summary>
    [Authorize("init")]
    [HttpPost("Provider/TMDB")]
    public async Task<ActionResult<ProviderTestResult>> SetTmdbProvider([FromBody] TmdbProviderInput input, CancellationToken ct)
    {
        var settings = SettingsProvider.GetSettings();
        settings.TMDB.UserApiKey = string.IsNullOrWhiteSpace(input.ApiKey) ? null : input.ApiKey.Trim();
        SettingsProvider.SaveSettings(settings);

        if (string.IsNullOrWhiteSpace(settings.TMDB.UserApiKey))
            return Ok(new ProviderTestResult(true, null));

        var (success, error) = await _tmdbService.TestApiKey(settings.TMDB.UserApiKey, ct);
        return Ok(new ProviderTestResult(success, error));
    }

    /// <summary>
    /// Gets the current TVDB provider setup state.
    /// </summary>
    [Authorize("init")]
    [HttpGet("Provider/TVDB")]
    public ActionResult<TvdbProviderSetupInfo> GetTvdbProvider()
    {
        var tvdb = SettingsProvider.GetSettings().TVDB;
        return new TvdbProviderSetupInfo
        {
            Enabled = tvdb.Enabled,
            Configured = !string.IsNullOrWhiteSpace(tvdb.ApiKey),
        };
    }

    /// <summary>
    /// Saves the TVDB API key and PIN, enables the provider, and tests connectivity.
    /// </summary>
    [Authorize("init")]
    [HttpPost("Provider/TVDB")]
    public async Task<ActionResult<ProviderTestResult>> SetTvdbProvider([FromBody] TvdbProviderInput input, CancellationToken ct)
    {
        var settings = SettingsProvider.GetSettings();
        settings.TVDB.Enabled = input.Enabled;
        settings.TVDB.ApiKey = string.IsNullOrWhiteSpace(input.ApiKey) ? null : input.ApiKey.Trim();
        settings.TVDB.Pin = string.IsNullOrWhiteSpace(input.Pin) ? null : input.Pin.Trim();
        SettingsProvider.SaveSettings(settings);

        if (!input.Enabled || string.IsNullOrWhiteSpace(settings.TVDB.ApiKey))
            return Ok(new ProviderTestResult(true, null));

        var (success, error) = await _tvdbService.TestCredentials(settings.TVDB.ApiKey, settings.TVDB.Pin, ct);
        return Ok(new ProviderTestResult(success, error));
    }

    /// <summary>
    /// Tests the supplied TMDB API key without saving it.
    /// </summary>
    [Authorize(Roles = "admin")]
    [HttpPost("Provider/TMDB/Test")]
    public async Task<ActionResult<ProviderTestResult>> TestTmdbProvider([FromBody] TmdbProviderInput input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.ApiKey))
            return BadRequest("API key is required.");
        var (success, error) = await _tmdbService.TestApiKey(input.ApiKey.Trim(), ct);
        return Ok(new ProviderTestResult(success, error));
    }

    /// <summary>
    /// Tests the supplied TVDB API key and PIN without saving them.
    /// </summary>
    [Authorize(Roles = "admin")]
    [HttpPost("Provider/TVDB/Test")]
    public async Task<ActionResult<ProviderTestResult>> TestTvdbProvider([FromBody] TvdbProviderInput input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.ApiKey))
            return BadRequest("API key is required.");
        var (success, error) = await _tvdbService.TestCredentials(input.ApiKey.Trim(), input.Pin, ct);
        return Ok(new ProviderTestResult(success, error));
    }

    /// <summary>
    /// Gets the current IMDb provider setup state.
    /// </summary>
    [Authorize("init")]
    [HttpGet("Provider/IMDB")]
    public ActionResult<ImdbProviderSetupInfo> GetImdbProvider()
    {
        var imdb = SettingsProvider.GetSettings().IMDb;
        return new ImdbProviderSetupInfo { Enabled = imdb.Enabled, DatasetPath = imdb.DatasetPath };
    }

    /// <summary>
    /// Saves the IMDb provider settings.
    /// </summary>
    [Authorize("init")]
    [HttpPost("Provider/IMDB")]
    public ActionResult SetImdbProvider([FromBody] ImdbProviderInput input)
    {
        var settings = SettingsProvider.GetSettings();
        settings.IMDb.Enabled = input.Enabled;
        settings.IMDb.DatasetPath = input.DatasetPath?.Trim() ?? string.Empty;
        SettingsProvider.SaveSettings(settings);
        return Ok();
    }

    /// <summary>
    /// Gets the current telemetry (Sentry error reporting) opt-out state.
    /// </summary>
    [Authorize("init")]
    [HttpGet("Telemetry")]
    public ActionResult<TelemetryInfo> GetTelemetry()
        => new TelemetryInfo { OptOut = SettingsProvider.GetSettings().SentryOptOut };

    /// <summary>
    /// Sets the telemetry (Sentry error reporting) opt-out preference.
    /// </summary>
    [Authorize("init")]
    [HttpPost("Telemetry")]
    public ActionResult SetTelemetry([FromBody] TelemetryInput input)
    {
        var settings = SettingsProvider.GetSettings();
        settings.SentryOptOut = input.OptOut;
        SettingsProvider.SaveSettings(settings);
        return Ok();
    }

    /// <summary>
    /// Verifies that the supplied credentials are valid and, if so, returns an API key.
    /// Only callable once the server has fully started.
    /// </summary>
    [Authorize("init")]
    [InitFriendly]
    [HttpPost("VerifyLogin")]
    public async Task<ActionResult<LoginVerificationResult>> VerifyLogin([FromBody] LoginInput input)
    {
        if (!_systemService.IsStarted)
            return Ok(new LoginVerificationResult(false, null, "Server has not finished starting yet."));

        if (string.IsNullOrWhiteSpace(input.Username) || string.IsNullOrWhiteSpace(input.Device))
            return BadRequest("Username and Device are required.");

        var user = _userService.AuthenticateUser(input.Username.Trim(), (input.Password ?? string.Empty).Trim());
        if (user is null)
            return Ok(new LoginVerificationResult(false, null, "Invalid username or password."));

        var apiKey = await _userService.GenerateRestApiTokenForUser(user, input.Device.Trim());
        return Ok(new LoginVerificationResult(true, apiKey, null));
    }

    #region Repair

    /// <summary>
    /// Returns a diagnostic snapshot of DB health, folder count, Plex/TMDB config, and WebUI version.
    /// </summary>
    [Authorize("admin")]
    [HttpGet("Repair/Status")]
    public ActionResult<RepairStatusResult> GetRepairStatus()
    {
        var settings = SettingsProvider.GetSettings();

        var dbOk = settings.Database.Type switch
        {
            Constants.DatabaseType.MySQL => new MySQL(_systemService).TestConnection(),
            Constants.DatabaseType.SQLServer => new SQLServer(_systemService).TestConnection(),
            _ => new SQLite(_systemService).TestConnection(),
        };

        var folderCount = RepoFactory.DaCollectorManagedFolder.GetAll().Count;
        var plexConfigured = !string.IsNullOrWhiteSpace(settings.Plex.TargetToken)
            && !string.IsNullOrWhiteSpace(settings.Plex.TargetBaseUrl);
        var tmdbConfigured = !string.IsNullOrWhiteSpace(settings.TMDB.UserApiKey);

        var currentVer = _webUIUpdateService.LoadWebComponentVersionInformation();
        var includedVer = _webUIUpdateService.LoadIncludedWebComponentVersionInformation();
        var webUIUpToDate = includedVer is null
            || (currentVer is not null && new SemverVersionComparer().Compare(includedVer.Version, currentVer.Version) <= 0);

        return Ok(new RepairStatusResult
        {
            DatabaseOk = dbOk,
            ManagedFolderCount = folderCount,
            PlexConfigured = plexConfigured,
            TmdbConfigured = tmdbConfigured,
            WebUIVersion = currentVer?.Version.ToSemanticVersioningString(),
            WebUIBundledVersion = includedVer?.Version.ToSemanticVersioningString(),
            WebUIUpToDate = webUIUpToDate,
        });
    }

    /// <summary>
    /// Force-copies the bundled WebUI files to the data directory, replacing the current installation.
    /// Takes effect immediately — no restart required.
    /// </summary>
    [Authorize("admin")]
    [HttpPost("Repair/WebUI")]
    public ActionResult RepairWebUI()
    {
        var webUIDir = new DirectoryInfo(ApplicationPaths.Instance.WebPath);
        var backupDir = new DirectoryInfo(Path.Combine(ApplicationPaths.Instance.ApplicationPath, "webui"));

        if (!backupDir.Exists)
            return BadRequest("No bundled WebUI files found to restore from.");

        try
        {
            CopyWebUIFilesRecursively(backupDir, webUIDir);
            return Ok();
        }
        catch (Exception e)
        {
            return StatusCode(500, e.Message);
        }
    }

    private static void CopyWebUIFilesRecursively(DirectoryInfo source, DirectoryInfo target)
    {
        if (target.Exists) target.Delete(recursive: true);
        target.Create();
        foreach (var dir in source.GetDirectories())
            CopyWebUIFilesRecursively(dir, target.CreateSubdirectory(dir.Name));
        foreach (var file in source.GetFiles())
            file.CopyTo(Path.Combine(target.FullName, file.Name));
    }

    #endregion

    #region Setup Input/Output Models

    public record TmdbProviderInput(string? ApiKey);

    public record TvdbProviderInput(bool Enabled, string? ApiKey, string? Pin);

    public record ImdbProviderInput(bool Enabled, string? DatasetPath);

    public record LoginInput(string Username, string? Password, string Device);

    public record ProviderTestResult(bool Success, string? Error);

    public record ProviderSetupInfo
    {
        public bool Configured { get; init; }
    }

    public record TvdbProviderSetupInfo
    {
        public bool Enabled { get; init; }
        public bool Configured { get; init; }
    }

    public record ImdbProviderSetupInfo
    {
        public bool Enabled { get; init; }
        public string DatasetPath { get; init; } = string.Empty;
    }

    public record LoginVerificationResult(bool Ready, string? ApiKey, string? Error);

    public record TelemetryInput(bool OptOut);

    public record TelemetryInfo
    {
        public bool OptOut { get; init; }
    }

    public record RepairStatusResult
    {
        public bool DatabaseOk { get; init; }
        public int ManagedFolderCount { get; init; }
        public bool PlexConfigured { get; init; }
        public bool TmdbConfigured { get; init; }
        public string? WebUIVersion { get; init; }
        public string? WebUIBundledVersion { get; init; }
        public bool WebUIUpToDate { get; init; }
    }

    #endregion
}
