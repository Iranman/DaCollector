using System.Collections.Generic;
using DaCollector.Abstractions.Metadata.Enums;

namespace DaCollector.Abstractions.Metadata.Containers;

/// <summary>
/// Represents an entity with yearly seasons.
/// </summary>
public interface IWithYearlySeasons
{
    /// <summary>
    /// Get all yearly seasons the entity was released in.
    /// </summary>
    IReadOnlyList<(int Year, YearlySeason Season)> YearlySeasons { get; }
}
