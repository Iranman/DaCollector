
using Microsoft.Extensions.Logging;

namespace DaCollector.Abstractions.Extensions;

/// <summary>
///   Extension methods for log levels.
/// </summary>
public static class LoggingExtensions
{
    extension(LogLevel level)
    {
        /// <summary>
        ///   Returns a string representation of the log level.
        /// </summary>
        /// <returns>
        ///   A string representation of the log level.
        /// </returns>
        public string ToNLogString()
            => level switch
            {
                LogLevel.Trace => "Trace",
                LogLevel.Debug => "Debug",
                LogLevel.Information => "Info",
                LogLevel.Warning => "Warn",
                LogLevel.Error => "Error",
                LogLevel.Critical => "Fatal",
                _ => "Off",
            };

        /// <summary>
        ///   Returns a short string representation of the log level.
        /// </summary>
        /// <returns>
        ///   A short string representation of the log level.
        /// </returns>
        public string ToShortString()
            => level switch
            {
                LogLevel.Trace => "VRB",
                LogLevel.Debug => "DBG",
                LogLevel.Information => "INF",
                LogLevel.Warning => "WRN",
                LogLevel.Error => "ERR",
                LogLevel.Critical => "FTL",
                _ => "NON",
            };
    }
}
