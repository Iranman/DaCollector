using System.Collections.Generic;
using DaCollector.Abstractions.Metadata.Anidb;
using DaCollector.Abstractions.Metadata.Containers;

namespace DaCollector.Abstractions.User;

/// <summary>
/// DaCollector user.
/// </summary>
public interface IUser : IWithPortraitImage
{
    /// <summary>
    /// Unique ID.
    /// </summary>
    int ID { get; }

    /// <summary>
    /// Username.
    /// </summary>
    string Username { get; }

    /// <summary>
    /// Indicates that the user is an administrator.
    /// </summary>
    bool IsAdmin { get; }

    /// <summary>
    /// Indicates that the user is an AniDB user.
    /// </summary>
    bool IsAnidbUser { get; }

    /// <summary>
    ///   The restricted tags for the user. Any series with any of these tags
    ///   will be hidden from the user in the REST API.
    /// </summary>
    IReadOnlyList<IAnidbTag> RestrictedTags { get; }
}
