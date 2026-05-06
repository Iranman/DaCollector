using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using DaCollector.Abstractions.Metadata.Enums;
using DaCollector.Abstractions.Metadata.Events;

#nullable enable
namespace DaCollector.Server.API.SignalR.Models;

public class EpisodeInfoUpdatedEventSignalRModel
{
    public EpisodeInfoUpdatedEventSignalRModel(EpisodeInfoUpdatedEventArgs eventArgs)
    {
        Source = eventArgs.EpisodeInfo.Source;
        Reason = eventArgs.Reason;
        EpisodeID = eventArgs.EpisodeInfo.ID;
        SeriesID = eventArgs.SeriesInfo.ID;
        DaCollectorEpisodeIDs = eventArgs.EpisodeInfo.DaCollectorEpisodeIDs;
        DaCollectorSeriesIDs = eventArgs.SeriesInfo.DaCollectorSeriesIDs;
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
    /// The provided metadata episode id.
    /// </summary>
    public int EpisodeID { get; }

    /// <summary>
    /// The provided metadata series id.
    /// </summary>
    public int SeriesID { get; }

    /// <summary>
    /// DaCollector episode ids affected by this update.
    /// </summary>
    public IReadOnlyList<int> DaCollectorEpisodeIDs { get; }

    /// <summary>
    /// DaCollector series ids affected by this update.
    /// </summary>
    public IReadOnlyList<int> DaCollectorSeriesIDs { get; }
}
