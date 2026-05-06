using System;
using DaCollector.Abstractions.Video.Release;

namespace DaCollector.Abstractions.Video.Events;

/// <summary>
/// Dispatched when a video release is saved or deleted.
/// </summary>
public class VideoReleaseSavedEventArgs : EventArgs
{
    /// <summary>
    /// The video.
    /// </summary>
    public required IVideo Video { get; init; }

    /// <summary>
    /// The release information for the video.
    /// </summary>
    public required IReleaseInfo ReleaseInfo { get; init; }
}
