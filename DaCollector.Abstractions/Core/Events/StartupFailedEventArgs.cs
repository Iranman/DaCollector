using System;
using DaCollector.Abstractions.Core.Exceptions;
using DaCollector.Abstractions.Core.Services;

namespace DaCollector.Abstractions.Core.Events;

/// <summary>
///   Event arguments for the <see cref="ISystemService.StartupFailed"/> event.
/// </summary>
/// <param name="exception">The exception that was thrown during startup.</param>
public class StartupFailedEventArgs(StartupFailedException exception) : EventArgs
{
    /// <summary>
    ///   The exception that was thrown during startup.
    /// </summary>
    public StartupFailedException Exception { get; } = exception;
}
