
namespace Shoko.Abstractions.Logging.Models;

/// <summary>
///   Known log file content formats.
/// </summary>
public enum LogFileFormat
{
    /// <summary>
    ///   Legacy line format (non-JSONL).
    /// </summary>
    Legacy = 0,

    /// <summary>
    ///   JSON Lines format.
    /// </summary>
    JsonL = 1,
}
