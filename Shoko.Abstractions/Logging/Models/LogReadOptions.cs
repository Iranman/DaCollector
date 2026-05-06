using Shoko.Abstractions.Logging.Services;

namespace Shoko.Abstractions.Logging.Models;

/// <summary>
///   Options for <see cref="ILogService.ReadLogFile"/> and
///   <see cref="ILogService.ReadRange"/>.
/// </summary>
public sealed class LogReadOptions : LogBaseOptions
{
    /// <summary>
    ///   Line offset. Default <c>0</c>.
    /// </summary>
    public uint Offset { get; set; }

    /// <summary>
    ///   Max entries to return. Set to <c>0</c> to disable the limit. Default
    ///   <c>100</c>.
    /// </summary>
    public uint Limit { get; set; } = 100;

    /// <summary>
    ///   When set to <c>true</c>, the log entries are sorted in descending
    ///   order, meaning that the most recent entries are returned first.
    /// </summary>
    public bool Descending { get; set; }
}
