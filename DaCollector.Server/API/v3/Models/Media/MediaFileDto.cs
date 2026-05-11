using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using DaCollector.Abstractions.Video.Media;
using DaCollector.Server.Media;
using DaCollector.Server.Models.DaCollector;

#nullable enable
namespace DaCollector.Server.API.v3.Models.Media;

public sealed class MediaFileDto
{
    [Required]
    public int FileID { get; init; }

    [Required]
    public long SizeBytes { get; init; }

    [Required]
    public bool IsIgnored { get; init; }

    [Required]
    public bool IsVariation { get; init; }

    public string? Resolution { get; init; }

    [Required]
    public TimeSpan Duration { get; init; }

    [Required]
    public IReadOnlyList<MediaFileHashDto> Hashes { get; init; } = [];

    [Required]
    public IReadOnlyList<MediaFileLocationDto> Locations { get; init; } = [];

    public MediaFileReviewStateDto? Review { get; init; }

    [Required]
    public DateTime CreatedAt { get; init; }

    [Required]
    public DateTime UpdatedAt { get; init; }

    public DateTime? ImportedAt { get; init; }

    public static MediaFileDto FromVideoLocal(VideoLocal file, MediaFileReviewStateDto? review, bool includeAbsolutePaths)
    {
        var mediaInfo = file.MediaInfo as IMediaInfo;
        return new()
        {
            FileID = file.VideoLocalID,
            SizeBytes = file.FileSize,
            IsIgnored = file.IsIgnored,
            IsVariation = file.IsVariation,
            Resolution = mediaInfo?.VideoStream?.Resolution,
            Duration = file.DurationTimeSpan,
            Hashes = file.Hashes.Count > 0
                ? file.Hashes.Select(hash => new MediaFileHashDto { Type = hash.Type, Value = hash.Value }).ToList()
                : [new() { Type = "ED2K", Value = file.Hash }],
            Locations = file.Places.Select(location => MediaFileLocationDto.FromPlace(location, includeAbsolutePaths)).ToList(),
            Review = review,
            CreatedAt = file.DateTimeCreated.ToUniversalTime(),
            UpdatedAt = file.DateTimeUpdated.ToUniversalTime(),
            ImportedAt = file.DateTimeImported?.ToUniversalTime(),
        };
    }
}

public sealed class MediaFileHashDto
{
    [Required]
    public string Type { get; init; } = string.Empty;

    [Required]
    public string Value { get; init; } = string.Empty;
}

public sealed class MediaFileLocationDto
{
    [Required]
    public int LocationID { get; init; }

    [Required]
    public int ManagedFolderID { get; init; }

    [Required]
    public string RelativePath { get; init; } = string.Empty;

    public string? AbsolutePath { get; init; }

    [Required]
    public bool IsAvailable { get; init; }

    public static MediaFileLocationDto FromPlace(VideoLocal_Place location, bool includeAbsolutePath) =>
        new()
        {
            LocationID = location.ID,
            ManagedFolderID = location.ManagedFolderID,
            RelativePath = location.RelativePath,
            AbsolutePath = includeAbsolutePath ? location.Path : null,
            IsAvailable = location.IsAvailable,
        };
}
