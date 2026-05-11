using System;
using System.Collections.Generic;
using System.Linq;
using DaCollector.Server.API.v3.Models.Common;
using DaCollector.Server.Models.DaCollector;
using DaCollector.Server.Models.Internal;
using DaCollector.Server.Parsing;
using DaCollector.Server.Repositories;
using DaCollector.Server.Repositories.Direct;

#nullable enable
namespace DaCollector.Server.Media;

public class MediaFileReviewService(
    FilenameParserService filenameParser,
    MediaFileReviewStateRepository reviewStateRepository
)
{
    private const int MaxPageSize = 500;

    public ListResult<MediaFileReviewItem> GetUnmatchedFiles(
        bool includeIgnored,
        bool includeBrokenCrossReferences,
        int page,
        int pageSize
    )
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var videos = RepoFactory.VideoLocal
            .GetVideosWithoutEpisode(includeBrokenCrossReferences)
            .ToList();

        if (includeIgnored)
        {
            var existingIDs = videos.Select(video => video.VideoLocalID).ToHashSet();
            videos.AddRange(RepoFactory.VideoLocal.GetIgnoredVideos().Where(video => existingIDs.Add(video.VideoLocalID)));
        }

        var ordered = videos
            .OrderBy(video => GetPrimaryParsePath(video), StringComparer.OrdinalIgnoreCase)
            .ToList();

        var total = ordered.Count;
        var items = ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(video => BuildReviewItem(video, refreshParse: false))
            .ToList();

        return new ListResult<MediaFileReviewItem>(total, items);
    }

    public MediaFileReviewItem? GetFileReview(int videoLocalID, bool refreshParse = false)
    {
        var video = RepoFactory.VideoLocal.GetByID(videoLocalID);
        return video is null ? null : BuildReviewItem(video, refreshParse);
    }

    public MediaFileReviewItem? IgnoreFile(int videoLocalID, string? reason)
    {
        var video = RepoFactory.VideoLocal.GetByID(videoLocalID);
        if (video is null)
            return null;

        video.IsIgnored = true;
        video.DateTimeUpdated = DateTime.Now;
        RepoFactory.VideoLocal.Save(video);

        var state = GetOrCreateState(video, refreshParse: false);
        var now = DateTime.Now;
        state.Status = MediaFileReviewStatus.Ignored;
        state.IgnoredReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        state.UpdatedAt = now;
        reviewStateRepository.Save(state);

        return BuildReviewItem(video, state);
    }

    public MediaFileReviewItem? UnignoreFile(int videoLocalID)
    {
        var video = RepoFactory.VideoLocal.GetByID(videoLocalID);
        if (video is null)
            return null;

        video.IsIgnored = false;
        video.DateTimeUpdated = DateTime.Now;
        RepoFactory.VideoLocal.Save(video);

        var state = GetOrCreateState(video, refreshParse: false);
        var now = DateTime.Now;
        state.Status = string.IsNullOrWhiteSpace(state.ManualProviderID)
            ? MediaFileReviewStatus.Pending
            : MediaFileReviewStatus.ManualMatch;
        state.IgnoredReason = null;
        state.UpdatedAt = now;
        reviewStateRepository.Save(state);

        return BuildReviewItem(video, state);
    }

    public MediaFileReviewItem? SetManualMatch(int videoLocalID, ManualFileMatchRequest request)
    {
        var video = RepoFactory.VideoLocal.GetByID(videoLocalID);
        if (video is null)
            return null;

        var state = GetOrCreateState(video, refreshParse: false);
        var now = DateTime.Now;
        state.Status = MediaFileReviewStatus.ManualMatch;
        state.ManualEntityType = request.EntityType.Trim();
        state.ManualEntityID = request.EntityID;
        state.ManualProvider = request.Provider.Trim();
        state.ManualProviderID = request.ProviderID.Trim();
        state.ManualTitle = string.IsNullOrWhiteSpace(request.Title) ? null : request.Title.Trim();
        state.Locked = request.Locked;
        state.IgnoredReason = null;
        state.UpdatedAt = now;
        reviewStateRepository.Save(state);

        if (video.IsIgnored)
        {
            video.IsIgnored = false;
            video.DateTimeUpdated = now;
            RepoFactory.VideoLocal.Save(video);
        }

        return BuildReviewItem(video, state);
    }

    public MediaFileReviewItem? ClearManualMatch(int videoLocalID)
    {
        var video = RepoFactory.VideoLocal.GetByID(videoLocalID);
        if (video is null)
            return null;

        var state = GetOrCreateState(video, refreshParse: false);
        var now = DateTime.Now;
        state.Status = video.IsIgnored ? MediaFileReviewStatus.Ignored : MediaFileReviewStatus.Pending;
        state.ManualEntityType = null;
        state.ManualEntityID = null;
        state.ManualProvider = null;
        state.ManualProviderID = null;
        state.ManualTitle = null;
        state.Locked = false;
        state.UpdatedAt = now;
        reviewStateRepository.Save(state);

        return BuildReviewItem(video, state);
    }

    private MediaFileReviewItem BuildReviewItem(VideoLocal video, bool refreshParse)
        => BuildReviewItem(video, GetOrCreateState(video, refreshParse));

    private MediaFileReviewItem BuildReviewItem(VideoLocal video, MediaFileReviewState state)
    {
        var locations = RepoFactory.VideoLocalPlace
            .GetByVideoLocal(video.VideoLocalID)
            .Select(MediaFileReviewLocation.FromPlace)
            .ToList();

        return new MediaFileReviewItem
        {
            FileID = video.VideoLocalID,
            Hash = video.Hash,
            FileSize = video.FileSize,
            IsIgnored = video.IsIgnored,
            PrimaryPath = locations.FirstOrDefault(location => !string.IsNullOrWhiteSpace(location.Path))?.Path
                ?? locations.FirstOrDefault()?.RelativePath
                ?? GetLegacyFileName(video),
            Locations = locations,
            Review = MediaFileReviewStateDto.FromState(state),
        };
    }

    private MediaFileReviewState GetOrCreateState(VideoLocal video, bool refreshParse)
    {
        var state = reviewStateRepository.GetByVideoLocalID(video.VideoLocalID);
        var now = DateTime.Now;

        if (state is null)
        {
            state = new MediaFileReviewState
            {
                VideoLocalID = video.VideoLocalID,
                Status = video.IsIgnored ? MediaFileReviewStatus.Ignored : MediaFileReviewStatus.Pending,
                CreatedAt = now,
                UpdatedAt = now,
                LastParsedAt = now,
            };
            ApplyParsedResult(video, state, now);
            reviewStateRepository.Save(state);
            return state;
        }

        if (refreshParse)
        {
            ApplyParsedResult(video, state, now);
            reviewStateRepository.Save(state);
        }

        return state;
    }

    private void ApplyParsedResult(VideoLocal video, MediaFileReviewState state, DateTime now)
    {
        var parsePath = GetPrimaryParsePath(video);
        state.ApplyParsedResult(filenameParser.Parse(parsePath), now);
    }

    private static string GetPrimaryParsePath(VideoLocal video)
        => video.FirstValidPlace?.Path
            ?? video.FirstValidPlace?.RelativePath
            ?? GetLegacyFileName(video)
            ?? string.Empty;

    private static string GetLegacyFileName(VideoLocal video)
    {
#pragma warning disable CS0618
        return video.FileName;
#pragma warning restore CS0618
    }
}

public sealed class MediaFileReviewItem
{
    public int FileID { get; init; }

    public string Hash { get; init; } = string.Empty;

    public long FileSize { get; init; }

    public bool IsIgnored { get; init; }

    public string PrimaryPath { get; init; } = string.Empty;

    public IReadOnlyList<MediaFileReviewLocation> Locations { get; init; } = [];

    public MediaFileReviewStateDto Review { get; init; } = new();
}

public sealed class MediaFileReviewLocation
{
    public int ID { get; init; }

    public int ManagedFolderID { get; init; }

    public string RelativePath { get; init; } = string.Empty;

    public string? Path { get; init; }

    public string FileName { get; init; } = string.Empty;

    public bool IsAvailable { get; init; }

    public static MediaFileReviewLocation FromPlace(VideoLocal_Place place) =>
        new()
        {
            ID = place.ID,
            ManagedFolderID = place.ManagedFolderID,
            RelativePath = place.RelativePath,
            Path = place.Path,
            FileName = place.FileName,
            IsAvailable = place.IsAvailable,
        };
}

public sealed class MediaFileReviewStateDto
{
    public int ReviewStateID { get; init; }

    public string Status { get; init; } = MediaFileReviewStatus.Pending;

    public string ParsedKind { get; init; } = ParsedMediaKind.Unknown.ToString();

    public string? ParsedTitle { get; init; }

    public int? ParsedYear { get; init; }

    public string? ParsedShowTitle { get; init; }

    public int? ParsedSeasonNumber { get; init; }

    public IReadOnlyList<int> ParsedEpisodeNumbers { get; init; } = [];

    public string? ParsedAirDate { get; init; }

    public IReadOnlyList<ExternalIdGuess> ParsedExternalIds { get; init; } = [];

    public string? ParsedQuality { get; init; }

    public string? ParsedSource { get; init; }

    public string? ParsedEdition { get; init; }

    public string? ParsedVideoCodec { get; init; }

    public string? ParsedAudioCodec { get; init; }

    public string? ParsedAudioChannels { get; init; }

    public IReadOnlyList<string> ParsedHdrFormats { get; init; } = [];

    public IReadOnlyList<string> ParsedWarnings { get; init; } = [];

    public string? ManualEntityType { get; init; }

    public int? ManualEntityID { get; init; }

    public string? ManualProvider { get; init; }

    public string? ManualProviderID { get; init; }

    public string? ManualTitle { get; init; }

    public bool Locked { get; init; }

    public string? IgnoredReason { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime UpdatedAt { get; init; }

    public DateTime LastParsedAt { get; init; }

    public static MediaFileReviewStateDto FromState(MediaFileReviewState state) =>
        new()
        {
            ReviewStateID = state.MediaFileReviewStateID,
            Status = state.Status,
            ParsedKind = state.ParsedKind,
            ParsedTitle = state.ParsedTitle,
            ParsedYear = state.ParsedYear,
            ParsedShowTitle = state.ParsedShowTitle,
            ParsedSeasonNumber = state.ParsedSeasonNumber,
            ParsedEpisodeNumbers = state.ParsedEpisodeNumbers,
            ParsedAirDate = state.ParsedAirDate,
            ParsedExternalIds = state.ParsedExternalIds,
            ParsedQuality = state.ParsedQuality,
            ParsedSource = state.ParsedSource,
            ParsedEdition = state.ParsedEdition,
            ParsedVideoCodec = state.ParsedVideoCodec,
            ParsedAudioCodec = state.ParsedAudioCodec,
            ParsedAudioChannels = state.ParsedAudioChannels,
            ParsedHdrFormats = state.ParsedHdrFormats,
            ParsedWarnings = state.ParsedWarnings,
            ManualEntityType = state.ManualEntityType,
            ManualEntityID = state.ManualEntityID,
            ManualProvider = state.ManualProvider,
            ManualProviderID = state.ManualProviderID,
            ManualTitle = state.ManualTitle,
            Locked = state.Locked,
            IgnoredReason = state.IgnoredReason,
            CreatedAt = state.CreatedAt,
            UpdatedAt = state.UpdatedAt,
            LastParsedAt = state.LastParsedAt,
        };
}

public sealed record ManualFileMatchRequest
{
    public string EntityType { get; init; } = string.Empty;

    public int? EntityID { get; init; }

    public string Provider { get; init; } = string.Empty;

    public string ProviderID { get; init; } = string.Empty;

    public string? Title { get; init; }

    public bool Locked { get; init; } = true;
}
