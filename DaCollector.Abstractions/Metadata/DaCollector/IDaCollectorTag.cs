using System.Collections.Generic;

namespace DaCollector.Abstractions.Metadata.DaCollector;

/// <summary>
/// A DaCollector tag.
/// </summary>
public interface IDaCollectorTag : ITag
{
    /// <summary>
    /// All DaCollector series the tag is set on.
    /// </summary>
    IReadOnlyList<IDaCollectorSeries> AllDaCollectorSeries { get; }
}

/// <summary>
/// A DaCollector tag with additional information for a single DaCollector series.
/// </summary>
public interface IDaCollectorTagForSeries : IDaCollectorTag
{
    /// <summary>
    /// The ID of the DaCollector series the tag is set on.
    /// </summary>
    int DaCollectorSeriesID { get; }

    /// <summary>
    /// A direct link to the DaCollector Seres metadata.
    /// </summary>
    IDaCollectorSeries DaCollectorSeries { get; }
}
