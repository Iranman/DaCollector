namespace DaCollector.Abstractions.Duplicates;

/// <summary>
/// Primary signal used to group possible duplicate media entries.
/// </summary>
public enum MediaDuplicateMatchType : int
{
    /// <summary>
    /// No duplicate signal was selected.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Two or more Plex entries point at the same media file path hash.
    /// </summary>
    PathHash = 1,

    /// <summary>
    /// Two or more Plex entries share an external provider ID.
    /// </summary>
    ProviderID = 2,

    /// <summary>
    /// Two or more Plex entries have the same normalized title and year.
    /// </summary>
    TitleYear = 3,
}
