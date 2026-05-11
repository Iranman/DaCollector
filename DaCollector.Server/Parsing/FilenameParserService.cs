using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

#nullable enable
namespace DaCollector.Server.Parsing;

public class FilenameParserService
{
    private static readonly Regex ExternalIdRegex = new(@"\{?\b(?<source>tmdb|tvdb|imdb)-(?<id>tt\d+|\d+)\b\}?", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex TvEpisodeRegex = new(@"^(?<show>.+?)[ ._-]+S(?<season>\d{1,2})E(?<episode>\d{1,3})(?:\s*(?:-|E)\s*E?(?<end>\d{1,3}))?", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex DateEpisodeRegex = new(@"^(?<show>.+?)[ ._-]+(?<date>\d{4}[-.]\d{2}[-.]\d{2})(?:[ ._-]|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex MovieYearRegex = new(@"^(?<title>.+?)[ ._-]*[\(\[]?(?<year>19\d{2}|20\d{2})[\)\]]?(?:[ ._-]|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex QualityRegex = new(@"\b(?<quality>2160p|1080p|720p|576p|480p|4k|uhd)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ChannelsRegex = new(@"\b(?<channels>[257][ .]1|1[ .]0|2[ .]0)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public ParsedFilenameResult Parse(string path)
    {
        var fileName = Path.GetFileName(path);
        var baseName = Path.GetFileNameWithoutExtension(fileName);
        var normalized = NormalizeSeparators(baseName);
        var result = new ParsedFilenameResult
        {
            OriginalPath = path,
            FileName = fileName,
            ExternalIds = ExtractExternalIds(path),
            Quality = ExtractQuality(normalized),
            Source = ExtractSource(normalized),
            Edition = ExtractEdition(normalized),
            VideoCodec = ExtractVideoCodec(normalized),
            AudioCodec = ExtractAudioCodec(normalized),
            AudioChannels = ExtractAudioChannels(normalized),
            HdrFormats = ExtractHdrFormats(normalized),
        };

        var withoutIds = RemoveExternalIds(normalized);
        if (TryParseTvEpisode(withoutIds, result, out var tvResult))
            return tvResult;
        if (TryParseDateEpisode(withoutIds, result, out tvResult))
            return tvResult;
        if (TryParseMovie(withoutIds, result, out var movieResult))
            return movieResult;

        return result with
        {
            Kind = ParsedMediaKind.Unknown,
            Title = CleanTitle(RemoveKnownTechnicalTokens(withoutIds)),
            Warnings = ["Unable to classify as movie or TV episode."],
        };
    }

    private static bool TryParseTvEpisode(string normalized, ParsedFilenameResult seed, out ParsedFilenameResult result)
    {
        var match = TvEpisodeRegex.Match(normalized);
        if (!match.Success)
        {
            result = seed;
            return false;
        }

        var episode = int.Parse(match.Groups["episode"].Value, CultureInfo.InvariantCulture);
        var episodes = new List<int> { episode };
        if (match.Groups["end"].Success && int.TryParse(match.Groups["end"].Value, CultureInfo.InvariantCulture, out var end) && end > episode)
        {
            for (var i = episode + 1; i <= end; i++)
                episodes.Add(i);
        }

        result = seed with
        {
            Kind = episodes.Count > 1 ? ParsedMediaKind.MultiEpisodeTvFile : ParsedMediaKind.TvEpisode,
            ShowTitle = CleanTitle(match.Groups["show"].Value),
            SeasonNumber = int.Parse(match.Groups["season"].Value, CultureInfo.InvariantCulture),
            EpisodeNumbers = episodes,
            Title = null,
            Year = null,
        };
        return true;
    }

    private static bool TryParseDateEpisode(string normalized, ParsedFilenameResult seed, out ParsedFilenameResult result)
    {
        var match = DateEpisodeRegex.Match(normalized);
        if (!match.Success)
        {
            result = seed;
            return false;
        }

        var rawDate = match.Groups["date"].Value.Replace('.', '-');
        if (!DateOnly.TryParseExact(rawDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var airDate))
        {
            result = seed;
            return false;
        }

        result = seed with
        {
            Kind = ParsedMediaKind.TvEpisode,
            ShowTitle = CleanTitle(match.Groups["show"].Value),
            AirDate = airDate,
            Title = null,
            Year = null,
        };
        return true;
    }

    private static bool TryParseMovie(string normalized, ParsedFilenameResult seed, out ParsedFilenameResult result)
    {
        var match = MovieYearRegex.Match(normalized);
        if (!match.Success)
        {
            result = seed;
            return false;
        }

        result = seed with
        {
            Kind = ParsedMediaKind.Movie,
            Title = CleanTitle(match.Groups["title"].Value),
            Year = int.Parse(match.Groups["year"].Value, CultureInfo.InvariantCulture),
            ShowTitle = null,
            SeasonNumber = null,
            EpisodeNumbers = [],
            AirDate = null,
        };
        return true;
    }

    private static IReadOnlyList<ExternalIdGuess> ExtractExternalIds(string path)
        => ExternalIdRegex.Matches(path)
            .Select(match => new ExternalIdGuess(match.Groups["source"].Value.ToUpperInvariant(), match.Groups["id"].Value))
            .Distinct()
            .ToList();

    private static string? ExtractQuality(string normalized)
    {
        var match = QualityRegex.Match(normalized);
        if (!match.Success)
            return null;

        return match.Groups["quality"].Value.ToLowerInvariant() switch
        {
            "4k" or "uhd" => "2160p",
            var value => value,
        };
    }

    private static string? ExtractSource(string normalized)
    {
        var text = normalized.ToLowerInvariant();
        if (text.Contains("uhd bluray", StringComparison.Ordinal) || text.Contains("uhd blu ray", StringComparison.Ordinal))
            return "UHD BluRay";
        if (text.Contains("blu ray", StringComparison.Ordinal) || text.Contains("bluray", StringComparison.Ordinal))
            return "BluRay";
        if (text.Contains("web dl", StringComparison.Ordinal) || text.Contains("web-dl", StringComparison.Ordinal))
            return "WEB-DL";
        if (text.Contains("webrip", StringComparison.Ordinal) || text.Contains("web rip", StringComparison.Ordinal))
            return "WEBRip";
        if (text.Contains("hdtv", StringComparison.Ordinal))
            return "HDTV";
        if (text.Contains("dvd", StringComparison.Ordinal))
            return "DVD";
        return null;
    }

    private static string? ExtractEdition(string normalized)
        => ContainsToken(normalized, "remux") ? "REMUX" : null;

    private static string? ExtractVideoCodec(string normalized)
    {
        var text = normalized.ToLowerInvariant();
        if (ContainsToken(text, "hevc") || ContainsToken(text, "x265") || ContainsToken(text, "h265") || ContainsToken(text, "h 265"))
            return "HEVC";
        if (ContainsToken(text, "avc") || ContainsToken(text, "x264") || ContainsToken(text, "h264") || ContainsToken(text, "h 264"))
            return "H.264";
        if (ContainsToken(text, "mpeg2"))
            return "MPEG-2";
        return null;
    }

    private static string? ExtractAudioCodec(string normalized)
    {
        var text = normalized.ToLowerInvariant();
        if (text.Contains("truehd", StringComparison.Ordinal) && ContainsToken(text, "atmos"))
            return "TrueHD Atmos";
        if (text.Contains("truehd", StringComparison.Ordinal))
            return "TrueHD";
        if (text.Contains("dts hd ma", StringComparison.Ordinal) || text.Contains("dts-hd ma", StringComparison.Ordinal))
            return "DTS-HD MA";
        if (ContainsToken(text, "dtsx") || text.Contains("dts x", StringComparison.Ordinal))
            return "DTS:X";
        if (ContainsToken(text, "eac3") || text.Contains("e ac3", StringComparison.Ordinal))
            return "EAC3";
        if (ContainsToken(text, "ac3"))
            return "AC3";
        if (ContainsToken(text, "aac"))
            return "AAC";
        if (ContainsToken(text, "dts"))
            return "DTS";
        return null;
    }

    private static string? ExtractAudioChannels(string normalized)
    {
        var match = ChannelsRegex.Match(normalized);
        return match.Success ? match.Groups["channels"].Value.Replace(' ', '.') : null;
    }

    private static IReadOnlyList<string> ExtractHdrFormats(string normalized)
    {
        var text = normalized.ToLowerInvariant();
        var formats = new List<string>();
        if (ContainsToken(text, "dv") || text.Contains("dolby vision", StringComparison.Ordinal))
            formats.Add("Dolby Vision");
        if (text.Contains("hdr10+", StringComparison.Ordinal))
            formats.Add("HDR10+");
        else if (ContainsToken(text, "hdr") || ContainsToken(text, "hdr10"))
            formats.Add("HDR");
        return formats;
    }

    private static string RemoveExternalIds(string value)
        => ExternalIdRegex.Replace(value, " ");

    private static string RemoveKnownTechnicalTokens(string value)
    {
        var tokens = new[]
        {
            "2160p", "1080p", "720p", "576p", "480p", "4k", "uhd", "bluray", "blu ray", "web dl", "web-dl",
            "webrip", "web rip", "hdtv", "dvd", "remux", "dv", "hdr", "hdr10", "hdr10+", "hevc", "x265",
            "h265", "h 265", "avc", "x264", "h264", "h 264", "truehd", "atmos", "dts", "aac", "ac3", "eac3",
        };
        return tokens.Aggregate(value, (current, token) => Regex.Replace(current, $@"\b{Regex.Escape(token)}\b", " ", RegexOptions.IgnoreCase));
    }

    private static string NormalizeSeparators(string value)
        => Regex.Replace(value.Replace('.', ' ').Replace('_', ' '), @"\s+", " ").Trim();

    private static string CleanTitle(string value)
    {
        value = RemoveKnownTechnicalTokens(value);
        value = Regex.Replace(value, @"[\[\]\(\)\{\}]+", " ");
        value = Regex.Replace(value, @"\s*-\s*$", " ");
        value = Regex.Replace(value, @"\s+", " ").Trim(' ', '-', '.', '_');
        return value;
    }

    private static bool ContainsToken(string value, string token)
        => Regex.IsMatch(value, $@"(^|[^a-z0-9]){Regex.Escape(token)}([^a-z0-9]|$)", RegexOptions.IgnoreCase);
}

public enum ParsedMediaKind
{
    Unknown,
    Movie,
    TvEpisode,
    MultiEpisodeTvFile,
}

public sealed record ParsedFilenameResult
{
    public required string OriginalPath { get; init; }

    public required string FileName { get; init; }

    public ParsedMediaKind Kind { get; init; } = ParsedMediaKind.Unknown;

    public string? Title { get; init; }

    public int? Year { get; init; }

    public string? ShowTitle { get; init; }

    public int? SeasonNumber { get; init; }

    public IReadOnlyList<int> EpisodeNumbers { get; init; } = [];

    public DateOnly? AirDate { get; init; }

    public IReadOnlyList<ExternalIdGuess> ExternalIds { get; init; } = [];

    public string? Quality { get; init; }

    public string? Source { get; init; }

    public string? Edition { get; init; }

    public string? VideoCodec { get; init; }

    public string? AudioCodec { get; init; }

    public string? AudioChannels { get; init; }

    public IReadOnlyList<string> HdrFormats { get; init; } = [];

    public IReadOnlyList<string> Warnings { get; init; } = [];
}

public sealed record ExternalIdGuess(string Source, string Id);
