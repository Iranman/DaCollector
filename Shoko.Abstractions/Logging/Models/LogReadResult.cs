using System.Collections.Generic;

namespace Shoko.Abstractions.Logging.Models;

/// <summary>
///   Paged log read result.
/// </summary>
public class LogReadResult
{
    /// <summary>
    ///   Next offset to continue reading from, or null if there are no more
    ///   entries.
    /// </summary>
    public required uint? NextOffset { get; init; }

    /// <summary>
    ///   Returned log entries for the requested page.
    /// </summary>
    public required IReadOnlyList<LogEntry> Entries { get; init; }
}
