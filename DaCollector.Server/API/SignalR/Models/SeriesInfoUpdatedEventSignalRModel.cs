using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using DaCollector.Abstractions.Metadata.Enums;
using DaCollector.Abstractions.Metadata.Events;

#nullable enable
namespace DaCollector.Server.API.SignalR.Models;

public class SeriesInfoUpdatedEventSignalRModel
{
    public SeriesInfoUpdatedEventSignalRModel(SeriesInfoUpdatedEventArgs eventArgs)
    {
        Source = eventArgs.SeriesInfo.Source;
        Reason = eventArgs.Reason;
        SeriesID = eventArgs.SeriesInfo.ID;
        DaCollectorSeriesIDs = eventArgs.SeriesInfo.DaCollectorSeriesIDs;
        Episodes = eventArgs.Episodes.Select(e => new SeriesInfoUpdatedEventEpisodeDetailsSignalRModel(e)).ToList();
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
    /// The provided metadata series id.
    /// </summary>
    public int SeriesID { get; }

    /// <summary>
    /// DaCollector series ids affected by this update.
    /// </summary>
    public IReadOnlyList<int> DaCollectorSeriesIDs { get; }

    /// <summary>
    /// The episodes that were added/updated/removed during this event.
    /// </summary>
    public IReadOnlyList<SeriesInfoUpdatedEventEpisodeDetailsSignalRModel> Episodes { get; }

    public class SeriesInfoUpdatedEventEpisodeDetailsSignalRModel
    {
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
        /// DaCollector episode ids affected by this update.
        /// </summary>
        public IReadOnlyList<int> DaCollectorEpisodeIDs { get; }

        public SeriesInfoUpdatedEventEpisodeDetailsSignalRModel(EpisodeInfoUpdatedEventArgs eventArgs)
        {
            Reason = eventArgs.Reason;
            EpisodeID = eventArgs.EpisodeInfo.ID;
            DaCollectorEpisodeIDs = eventArgs.EpisodeInfo.DaCollectorEpisodeIDs;
        }
    }
}
