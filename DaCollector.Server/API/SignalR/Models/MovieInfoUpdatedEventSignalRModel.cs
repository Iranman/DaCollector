using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using DaCollector.Abstractions.Metadata.Enums;
using DaCollector.Abstractions.Metadata.Events;

#nullable enable
namespace DaCollector.Server.API.SignalR.Models;

public class MovieInfoUpdatedEventSignalRModel
{
    public MovieInfoUpdatedEventSignalRModel(MovieInfoUpdatedEventArgs eventArgs)
    {
        Source = eventArgs.MovieInfo.Source;
        Reason = eventArgs.Reason;
        MovieID = eventArgs.MovieInfo.ID;
        DaCollectorEpisodeIDs = eventArgs.MovieInfo.DaCollectorEpisodeIDs;
        DaCollectorSeriesIDs = eventArgs.MovieInfo.DaCollectorSeriesIDs;
    }

    /// <summary>
    /// The provider metadata source.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public DataSource Source { get; }

    /// <summary>
    /// The update reason.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public UpdateReason Reason { get; }

    /// <summary>
    /// The provided metadata movie id.
    /// </summary>
    public int MovieID { get; }

    /// <summary>
    /// DaCollector episode ids affected by this update.
    /// </summary>
    public IReadOnlyList<int> DaCollectorEpisodeIDs { get; }

    /// <summary>
    /// DaCollector series ids affected by this update.
    /// </summary>
    public IReadOnlyList<int> DaCollectorSeriesIDs { get; }
}
