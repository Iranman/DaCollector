using System;
using System.Collections.Generic;
using System.Text.Json;
using DaCollector.Server.Parsing;

#nullable enable
namespace DaCollector.Server.Models.Internal;

public class MediaFileReviewState
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public virtual int MediaFileReviewStateID { get; set; }

    public virtual int VideoLocalID { get; set; }

    public virtual string Status { get; set; } = MediaFileReviewStatus.Pending;

    public virtual string ParsedKind { get; set; } = ParsedMediaKind.Unknown.ToString();

    public virtual string? ParsedTitle { get; set; }

    public virtual int? ParsedYear { get; set; }

    public virtual string? ParsedShowTitle { get; set; }

    public virtual int? ParsedSeasonNumber { get; set; }

    public virtual string ParsedEpisodeNumbersJson { get; set; } = "[]";

    public virtual string? ParsedAirDate { get; set; }

    public virtual string ParsedExternalIdsJson { get; set; } = "[]";

    public virtual string? ParsedQuality { get; set; }

    public virtual string? ParsedSource { get; set; }

    public virtual string? ParsedEdition { get; set; }

    public virtual string? ParsedVideoCodec { get; set; }

    public virtual string? ParsedAudioCodec { get; set; }

    public virtual string? ParsedAudioChannels { get; set; }

    public virtual string ParsedHdrFormatsJson { get; set; } = "[]";

    public virtual string ParsedWarningsJson { get; set; } = "[]";

    public virtual string? ManualEntityType { get; set; }

    public virtual int? ManualEntityID { get; set; }

    public virtual string? ManualProvider { get; set; }

    public virtual string? ManualProviderID { get; set; }

    public virtual string? ManualTitle { get; set; }

    public virtual bool Locked { get; set; }

    public virtual string? IgnoredReason { get; set; }

    public virtual DateTime CreatedAt { get; set; }

    public virtual DateTime UpdatedAt { get; set; }

    public virtual DateTime LastParsedAt { get; set; }

    public virtual IReadOnlyList<int> ParsedEpisodeNumbers =>
        DeserializeList<int>(ParsedEpisodeNumbersJson);

    public virtual IReadOnlyList<ExternalIdGuess> ParsedExternalIds =>
        DeserializeList<ExternalIdGuess>(ParsedExternalIdsJson);

    public virtual IReadOnlyList<string> ParsedHdrFormats =>
        DeserializeList<string>(ParsedHdrFormatsJson);

    public virtual IReadOnlyList<string> ParsedWarnings =>
        DeserializeList<string>(ParsedWarningsJson);

    public virtual void ApplyParsedResult(ParsedFilenameResult result, DateTime now)
    {
        ParsedKind = result.Kind.ToString();
        ParsedTitle = result.Title;
        ParsedYear = result.Year;
        ParsedShowTitle = result.ShowTitle;
        ParsedSeasonNumber = result.SeasonNumber;
        ParsedEpisodeNumbersJson = Serialize(result.EpisodeNumbers);
        ParsedAirDate = result.AirDate?.ToString("yyyy-MM-dd");
        ParsedExternalIdsJson = Serialize(result.ExternalIds);
        ParsedQuality = result.Quality;
        ParsedSource = result.Source;
        ParsedEdition = result.Edition;
        ParsedVideoCodec = result.VideoCodec;
        ParsedAudioCodec = result.AudioCodec;
        ParsedAudioChannels = result.AudioChannels;
        ParsedHdrFormatsJson = Serialize(result.HdrFormats);
        ParsedWarningsJson = Serialize(result.Warnings);
        LastParsedAt = now;
        UpdatedAt = now;
    }

    private static string Serialize<T>(IReadOnlyCollection<T> values)
        => JsonSerializer.Serialize(values, JsonOptions);

    private static IReadOnlyList<T> DeserializeList<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<T>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}

public static class MediaFileReviewStatus
{
    public const string Pending = "Pending";
    public const string Ignored = "Ignored";
    public const string ManualMatch = "ManualMatch";
}
