using System.Collections.Generic;
using DaCollector.Abstractions.Metadata.Containers;

namespace DaCollector.Abstractions.Metadata.DaCollector;

/// <summary>
/// DaCollector group metadata.
/// </summary>
public interface IDaCollectorGroup : ICollection, IMetadata<int>, IWithCreationDate, IWithUpdateDate
{
    /// <summary>
    /// DaCollector Group ID.
    /// </summary>
    new int ID { get; }

    /// <summary>
    /// The id of the direct parent group if the group is a child-group.
    /// </summary>
    int? ParentGroupID { get; }

    /// <summary>
    /// The id of the top-level group this group belongs to. It can refer to
    /// itself if it is atop-level group.
    /// </summary>
    int TopLevelGroupID { get; }

    /// <summary>
    /// The main series id for the group, be it automatically selected or
    /// a configured by a user.
    /// </summary>
    int MainSeriesID { get; }

    /// <summary>
    /// Indicates that the user have configured a main series for the group set.
    /// </summary>
    bool HasConfiguredMainSeries { get; }

    /// <summary>
    /// Indicates that the group has a custom title set.
    /// </summary>
    bool HasCustomTitle { get; }

    /// <summary>
    /// Indicates that the group have a custom description set.
    /// </summary>
    bool HasCustomDescription { get; }

    /// <summary>
    /// The direct parent of the group if the group is a child-group.
    /// </summary>
    IDaCollectorGroup? ParentGroup { get; }

    /// <summary>
    /// The top-level group this group belongs to. It can refer to itself if it
    /// is a top-level group.
    /// </summary>
    IDaCollectorGroup TopLevelGroup { get; }

    /// <summary>
    /// All child groups directly within the group, unordered.
    /// </summary>
    IReadOnlyList<IDaCollectorGroup> Groups { get; }

    /// <summary>
    /// All child groups directly within the group and within all child groups,
    /// unordered.
    /// </summary>
    IReadOnlyList<IDaCollectorGroup> AllGroups { get; }

    /// <summary>
    /// The main series within the group. It can be auto-selected (when
    /// auto-grouping is enabled) or user overwritten, and will fallback to the
    /// earliest airing series within the group or any child-groups if nothing
    /// is selected.
    /// </summary>
    IDaCollectorSeries MainSeries { get; }

    /// <summary>
    /// The series directly within the group, ordered by air-date.
    /// </summary>
    IReadOnlyList<IDaCollectorSeries> Series { get; }

    /// <summary>
    /// All series directly within the group and within all child-groups (if
    /// any), ordered by air-date.
    /// </summary>
    IReadOnlyList<IDaCollectorSeries> AllSeries { get; }
}
