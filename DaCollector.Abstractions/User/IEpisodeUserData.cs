using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using DaCollector.Abstractions.Metadata.DaCollector;

namespace DaCollector.Abstractions.User;

/// <summary>
///   Represents user-specific data associated with a DaCollector episode.
/// </summary>
public interface IEpisodeUserData : IUserData
{
    /// <summary>
    ///   Gets the ID of the DaCollector series.
    /// </summary>
    int SeriesID { get; }

    /// <summary>
    ///   Gets the ID of the DaCollector episode.
    /// </summary>
    int EpisodeID { get; }

    #region Episode Data

    /// <summary>
    ///   Gets the number of times the episode has been played for the user,
    ///   locally or otherwise.
    /// </summary>
    int PlaybackCount { get; }

    /// <summary>
    ///   Gets the date and time when the episode was last played to completion,
    ///   locally or otherwise.
    /// </summary>
    DateTime? LastPlayedAt { get; }

    /// <summary>
    ///   Indicates that the user has marked the episode as favorite.
    /// </summary>
    bool IsFavorite { get; }

    /// <summary>
    ///   The unique tags assigned to the episode by the user.
    /// </summary>
    IReadOnlyList<string> UserTags { get; }

    /// <summary>
    ///   The user rating, on a scale of 1-10 with a maximum of 1 decimal places, or <c>null</c> if unrated.
    /// </summary>
    double? UserRating { get; }

    #endregion

    #region Video Data

    /// <summary>
    ///   Gets the date and time when a video linked to the episode was last
    ///   played to completion.
    /// </summary>
    DateTime? LastVideoPlayedAt { get; }

    /// <summary>
    ///   Gets the date and time when the user data for a video linked to the
    ///   episode was last updated, regardless of if it was watched to
    ///   completion or not.
    /// </summary>
    DateTime? LastVideoUpdatedAt { get; }

    #endregion

    /// <summary>
    ///   Indicates that the episode has been watched to completion at least
    ///   once by the user, be it locally or otherwise.
    /// </summary>
    bool IsWatched => LastPlayedAt.HasValue || PlaybackCount > 0;

    /// <summary>
    ///   Indicates that the user has rated the episode.
    /// </summary>
    [MemberNotNullWhen(true, nameof(UserRating))]
    bool HasUserRating => UserRating.HasValue;

    /// <summary>
    ///   Gets the DaCollector Series associated with this user data.
    /// </summary>
    IDaCollectorSeries? Series { get; }

    /// <summary>
    ///   Gets the DaCollector Episode associated with this user data.
    /// </summary>
    IDaCollectorEpisode? Episode { get; }
}
