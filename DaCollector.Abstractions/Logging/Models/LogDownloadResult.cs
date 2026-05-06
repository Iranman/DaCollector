using System.IO;

namespace DaCollector.Abstractions.Logging.Models;

/// <summary>
///   Metadata and stream for downloading a log file.
/// </summary>
public class LogDownloadResult
{
    /// <summary>
    ///   Suggested filename for the client.
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    ///   Content type for the stream.
    /// </summary>
    public required string ContentType { get; init; }

    /// <summary>
    ///   Open stream to read download contents from.
    /// </summary>
    public required Stream Stream { get; init; }
}
