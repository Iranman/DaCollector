using System.Collections.Generic;
using DaCollector.Abstractions.Metadata.Enums;
using DaCollector.Abstractions.Metadata.Containers;

namespace DaCollector.Abstractions.Metadata;

/// <summary>
/// A studio.
/// </summary>
public interface IStudio : IMetadata<int>, IWithPortraitImage
{
    /// <summary>
    /// Parent entity ID.
    /// </summary>
    int ParentID { get; }

    /// <summary>
    /// The name of the studio.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// The original name of the studio.
    /// </summary>
    string? OriginalName { get; }

    /// <summary>
    /// The type of studio.
    /// </summary>
    StudioType StudioType { get; }

    /// <summary>
    /// Parent metadata entity.
    /// </summary>
    IMetadata<int>? Parent { get; }

    /// <summary>
    /// All locally known movie works by the studio.
    /// </summary>
    IEnumerable<IMovie> MovieWorks { get; }

    /// <summary>
    /// All locally known series works by the studio.
    /// </summary>
    IEnumerable<ISeries> SeriesWorks { get; }

    /// <summary>
    /// All locally known works by the studio.
    /// </summary>
    IEnumerable<IMetadata> Works { get; }
}

/// <summary>
/// A studio for a parent entity.
/// </summary>
public interface IStudio<TMetadata> : IStudio where TMetadata : IMetadata<int>
{
    /// <summary>
    /// Parent metadata entity.
    /// </summary>
    TMetadata? ParentOfType { get; }
}
