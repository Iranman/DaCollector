using System;
using DaCollector.Server.Services.ErrorHandling;

namespace DaCollector.Server.Exceptions;

[SentryInclude]
public class InvalidStateException : Exception
{
    public InvalidStateException(string message) : base(message) { }
}
