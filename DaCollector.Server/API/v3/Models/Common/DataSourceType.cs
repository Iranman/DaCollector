using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace DaCollector.Server.API.v3.Models.Common;

/// <summary>
/// Available data sources to chose from.
/// </summary>
/// <remarks>
/// Should be in sync with <see cref="global::DaCollector.Abstractions.Metadata.Enums.DataSource"/>.
/// </remarks>
[JsonConverter(typeof(StringEnumConverter))]
public enum DataSourceType
{
    /// <summary>
    /// AniDB.
    /// </summary>
    AniDB = 0,

    /// <summary>
    /// The Movie DataBase (TMDB).
    /// </summary>
    TMDB = 1,
}
