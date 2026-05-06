using System.Collections.Generic;
using DaCollector.Abstractions.Metadata.Containers;

namespace DaCollector.Abstractions.Metadata.DaCollector;

/// <summary>
/// Fake "season" for the DaCollector series.
/// </summary>
public interface IDaCollectorSeason : ISeason, IWithCreationDate, IWithUpdateDate
{
    /// <summary>
    /// Get the DaCollector series info for the "season," if available.
    /// </summary>
    new IDaCollectorSeries Series { get; }

    /// <summary>
    /// All episodes for the DaCollector series for the fake "season."
    /// </summary>
    new IReadOnlyList<IDaCollectorEpisode> Episodes { get; }
}
