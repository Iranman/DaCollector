using System;
using System.Globalization;
using System.Text.Json;

#nullable enable
namespace DaCollector.Server.Models.TVDB;

public class TVDB_Episode
{
    public int TVDB_EpisodeID { get; set; }

    public int TvdbEpisodeID { get; set; }

    public int TvdbShowID { get; set; }

    public int? TvdbSeasonID { get; set; }

    public int SeasonNumber { get; set; }

    public int EpisodeNumber { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Overview { get; set; } = string.Empty;

    public int? RuntimeMinutes { get; set; }

    public DateOnly? AiredAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime LastUpdatedAt { get; set; }

    public TVDB_Episode() { }

    public TVDB_Episode(int tvdbEpisodeId, int tvdbShowId)
    {
        TvdbEpisodeID = tvdbEpisodeId;
        TvdbShowID = tvdbShowId;
        CreatedAt = DateTime.Now;
        LastUpdatedAt = CreatedAt;
    }

    public bool Populate(JsonElement data, int tvdbShowId)
    {
        var name = GetString(data, "name") ?? Name;
        var overview = GetString(data, "overview") ?? Overview;
        var seasonNumber = GetInt(data, "seasonNumber") ?? GetInt(data, "airedSeason") ?? SeasonNumber;
        var episodeNumber = GetInt(data, "number") ?? GetInt(data, "airedEpisodeNumber") ?? EpisodeNumber;
        var runtime = GetInt(data, "runtime");
        var aired = ParseDate(GetString(data, "aired"));

        var updated = false;
        if (TvdbShowID != tvdbShowId) { TvdbShowID = tvdbShowId; updated = true; }
        if (Name != name) { Name = name; updated = true; }
        if (Overview != overview) { Overview = overview; updated = true; }
        if (SeasonNumber != seasonNumber) { SeasonNumber = seasonNumber; updated = true; }
        if (EpisodeNumber != episodeNumber) { EpisodeNumber = episodeNumber; updated = true; }
        if (RuntimeMinutes != runtime) { RuntimeMinutes = runtime; updated = true; }
        if (AiredAt != aired) { AiredAt = aired; updated = true; }
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

    private static DateOnly? ParseDate(string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return null;
        if (DateOnly.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return d;
        return null;
    }
}
