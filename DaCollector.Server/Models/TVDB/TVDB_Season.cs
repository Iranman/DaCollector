using System;
using System.Globalization;
using System.Text.Json;

#nullable enable
namespace DaCollector.Server.Models.TVDB;

public class TVDB_Season
{
    public int TVDB_SeasonID { get; set; }

    public int TvdbSeasonID { get; set; }

    public int TvdbShowID { get; set; }

    public int SeasonNumber { get; set; }

    public string SeasonType { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Overview { get; set; } = string.Empty;

    public int EpisodeCount { get; set; }

    public int? Year { get; set; }

    public string? PosterPath { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime LastUpdatedAt { get; set; }

    public TVDB_Season() { }

    public TVDB_Season(int tvdbSeasonId, int tvdbShowId)
    {
        TvdbSeasonID = tvdbSeasonId;
        TvdbShowID = tvdbShowId;
        CreatedAt = DateTime.Now;
        LastUpdatedAt = CreatedAt;
    }

    public bool Populate(JsonElement data, int episodeCount = 0)
    {
        var name = GetString(data, "name") ?? Name;
        var overview = GetString(data, "overview") ?? Overview;
        var seasonNumber = GetInt(data, "number") ?? SeasonNumber;
        var seasonType = GetNestedString(data, "type", "name") ?? GetString(data, "type") ?? SeasonType;
        var year = GetInt(data, "year");
        var poster = GetString(data, "image");

        var updated = false;
        if (Name != name) { Name = name; updated = true; }
        if (Overview != overview) { Overview = overview; updated = true; }
        if (SeasonNumber != seasonNumber) { SeasonNumber = seasonNumber; updated = true; }
        if (SeasonType != seasonType) { SeasonType = seasonType; updated = true; }
        if (EpisodeCount != episodeCount) { EpisodeCount = episodeCount; updated = true; }
        if (Year != year) { Year = year; updated = true; }
        if (PosterPath != poster) { PosterPath = poster; updated = true; }
        if (updated)
            LastUpdatedAt = DateTime.Now;
        return updated;
    }

    private static string? GetString(JsonElement el, string key)
    {
        if (el.TryGetProperty(key, out var prop) && prop.ValueKind is JsonValueKind.String)
            return prop.GetString();
        return null;
    }

    private static string? GetNestedString(JsonElement el, string key1, string key2)
    {
        if (el.TryGetProperty(key1, out var nested) && nested.ValueKind is JsonValueKind.Object)
            return GetString(nested, key2);
        return null;
    }

    private static int? GetInt(JsonElement el, string key)
    {
        if (el.TryGetProperty(key, out var prop))
        {
            if (prop.ValueKind is JsonValueKind.Number && prop.TryGetInt32(out var i))
                return i;
            if (prop.ValueKind is JsonValueKind.String && int.TryParse(prop.GetString(), out i))
                return i;
        }
        return null;
    }
}
