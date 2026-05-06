using System;
using DaCollector.Server.Services.ErrorHandling;

namespace DaCollector.Server.Providers.AniDB.UDP.Exceptions;

[SentryIgnore]
public class LoginFailedException : Exception
{
}
