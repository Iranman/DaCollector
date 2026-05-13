using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using DaCollector.Abstractions.Config.Attributes;
using DaCollector.Abstractions.Config.Enums;
using DaCollector.Abstractions.Plugin;

namespace DaCollector.Server.Settings;

/// <summary>
/// Configure settings related to the HTTP(S) hosting.
/// </summary>
public class WebSettings
{
    /// <summary>
    /// The port to listen on.
    /// </summary>
    [Display(Name = "Server Port")]
    [RequiresRestart]
    [EnvironmentVariable("DACOLLECTOR_PORT")]
    [DefaultValue(38111)]
    [Range(1, 65535, ErrorMessage = "Server Port must be between 1 and 65535")]
    public ushort Port { get; set; } = 38111;

    /// <summary>
    /// Automagically replace the current web ui with the included version if
    /// the current version is older then the included version.
    /// </summary>
    [Display(Name = "Auto Replace Web UI With Included Version")]
    [RequiresRestart]
    [EnvironmentVariable("DACOLLECTOR_WEBUI_AUTO_REPLACE")]
    [DefaultValue(true)]
    public bool AutoReplaceWebUIWithIncluded { get; set; } = true;

    /// <summary>
    /// Enable the Web UI. Disabling this will run the server in "headless"
    /// mode.
    /// </summary>
    [Display(Name = "Enable Web UI")]
    [RequiresRestart]
    [EnvironmentVariable("DACOLLECTOR_WEBUI_ENABLED")]
    [DefaultValue(true)]
    public bool EnableWebUI { get; set; } = true;

    /// <summary>
    /// The public path prefix for where to mount the Web UI.
    /// </summary>
    [Display(Name = "Web UI Prefix")]
    [RequiresRestart]
    [EnvironmentVariable("DACOLLECTOR_WEBUI_PREFIX")]
    [DefaultValue("webui")]
    public string WebUIPrefix { get; set; } = "webui";

    /// <summary>
    /// The public path formatted from <see cref="WebUIPrefix"/> for where to
    /// mount the Web UI.
    /// </summary>
    [JsonIgnore]
    public string WebUIPublicPath
    {
        get
        {
            var publicPath = WebUIPrefix;
            if (!publicPath.StartsWith('/'))
                publicPath = $"/{publicPath}";
            if (publicPath.EndsWith('/'))
                publicPath = publicPath[..^1];
            return publicPath;
        }
    }

    /// <summary>
    /// A relative path from the <see cref="IApplicationPaths.DataPath"/>
    /// to where the Web UI is installed, or an absolute path if you have it
    /// somewhere else. Will be used to populate the
    /// <see cref="IApplicationPaths.WebPath"/> field.
    /// </summary>
    [Visibility(Size = DisplayElementSize.Full)]
    [Display(Name = "Web UI Path")]
    [RequiresRestart]
    [EnvironmentVariable("DACOLLECTOR_WEBUI_PATH")]
    [DefaultValue("webui")]
    public string WebUIPath { get; set; } = "webui";

    /// <summary>
    /// Enable the Swagger UI.
    /// </summary>
    [Display(Name = "Enable Swagger UI")]
    [RequiresRestart]
    [EnvironmentVariable("DACOLLECTOR_SWAGGER_ENABLED")]
    [DefaultValue(true)]
    public bool EnableSwaggerUI { get; set; } = true;

    /// <summary>
    /// The public path prefix for where to mount the Swagger UI.
    /// </summary>
    [Display(Name = "Swagger UI Prefix")]
    [RequiresRestart]
    [EnvironmentVariable("DACOLLECTOR_SWAGGER_PREFIX")]
    [DefaultValue("swagger")]
    public string SwaggerUIPrefix { get; set; } = "swagger";

    /// <summary>
    ///   Enable the built-in index redirect available at <c>/</c>, redirecting
    ///   the user to the <seealso cref="WebUIPrefix"/>.
    /// </summary>
    [Visibility(Advanced = true)]
    [Display(Name = "Enable Index Redirect")]
    [RequiresRestart]
    [EnvironmentVariable("DACOLLECTOR_API_INDEX_REDIRECT_ENABLED")]
    [DefaultValue(true)]
    public bool EnableIndexRedirect { get; set; } = true;

    /// <summary>
    ///   Enable the built-in SignalR hubs available at <c>/signalr</c>.
    /// </summary>
    [Visibility(Advanced = true)]
    [Display(Name = "Enable SignalR")]
    [RequiresRestart]
    [EnvironmentVariable("DACOLLECTOR_API_SIGNALR_ENABLED")]
    [DefaultValue(true)]
    public bool EnableSignalR { get; set; } = true;

    /// <summary>
    ///   Enable the built-in authentication API available at <c>/api/auth</c>.
    /// </summary>
    [Visibility(Advanced = true)]
    [Display(Name = "Enable Auth API")]
    [RequiresRestart]
    [EnvironmentVariable("DACOLLECTOR_API_AUTH_ENABLED")]
    [DefaultValue(true)]
    public bool EnableAuthAPI { get; set; } = true;

    /// <summary>
    /// Enable the API v3 endpoints.
    /// </summary>
    [Display(Name = "Enable API v3")]
    [RequiresRestart]
    [EnvironmentVariable("DACOLLECTOR_API_V3_ENABLED")]
    [DefaultValue(true)]
    public bool EnableAPIv3 { get; set; } = true;

    /// <summary>
    ///   Enable the built-in legacy Plex API available at <c>/plex</c>, once
    ///   part of APIv2, but separated so that it can be toggled separately.
    /// </summary>
    [Badge("Deprecated", Theme = DisplayColorTheme.Warning)]
    [Visibility(Advanced = true)]
    [Display(Name = "Enable Legacy Plex API")]
    [RequiresRestart]
    [EnvironmentVariable("DACOLLECTOR_API_PLEX_LEGACY_ENABLED")]
    [DefaultValue(true)]
    public bool EnableLegacyPlexAPI { get; set; } = true;

    /// <summary>
    /// Allow anonymous file streaming in API v3.
    /// </summary>
    [Visibility(Advanced = true)]
    [Display(Name = "Allow Anonymous File Streaming in API v3")]
    [RequiresRestart]
    [EnvironmentVariable("DACOLLECTOR_API_V3_ALLOW_ANONYMOUS_FILE_STREAMING")]
    [DefaultValue(false)]
    public bool AllowAnonymousFileStreamingInAPIv3 { get; set; } = false;

    /// <summary>
    /// Always use the developer exceptions page, even in production.
    /// </summary>
    [Badge("Debug", Theme = DisplayColorTheme.Warning)]
    [Visibility(Advanced = true, Size = DisplayElementSize.Large)]
    [Display(Name = "Always Use Developer Exceptions")]
    [RequiresRestart]
    [EnvironmentVariable("DACOLLECTOR_WEB_DEVELOPER_EXCEPTIONS")]
    [DefaultValue(false)]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public bool AlwaysUseDeveloperExceptions { get; set; } = false;

    /// <summary>
    /// The name of the client repo to use.
    /// </summary>
    [Visibility(Advanced = true, Size = DisplayElementSize.Large)]
    [Display(Name = "Client Repo Name")]
    [RegularExpression(@"^[a-zA-Z0-9_\-\.]+/[a-zA-Z0-9_\-\.]+$")]
    [EnvironmentVariable("DACOLLECTOR_CLIENT_REPO")]
    [DefaultValue("Iranman/DaCollector-WebUI")]
    public string ClientRepoName { get; set; } = "Iranman/DaCollector-WebUI";

    /// <summary>
    /// The name of the server repo to use.
    /// </summary>
    [Badge("Advanced", Theme = DisplayColorTheme.Primary)]
    [Visibility(Advanced = true, Size = DisplayElementSize.Large)]
    [Display(Name = "Server Repo Name")]
    [EnvironmentVariable("DACOLLECTOR_SERVER_REPO")]
    [DefaultValue("Iranman/DaCollector")]
    public string ServerRepoName { get; set; } = "Iranman/DaCollector";
}
