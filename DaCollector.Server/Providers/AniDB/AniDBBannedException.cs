using System;
using DaCollector.Server.Services.ErrorHandling;

namespace DaCollector.Server.Providers.AniDB;

/// <summary>
/// An internal AniDB ban exception.
/// </summary>
[Serializable, SentryIgnore]
public class AniDBBannedException : Exception
{
    /// <summary>
    /// The type of ban that occurred.
    /// /// </summary>
    public required UpdateType BanType { get; init; }

    /// <summary>
    /// When the ban expires, in local time.
    /// </summary>
    public required DateTime? BanExpires { get; init; }
}
