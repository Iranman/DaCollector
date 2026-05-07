using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

#nullable enable
namespace DaCollector.Server.Models.TVDB;

public class TVDB_Movie
{
    public int TVDB_MovieID { get; set; }

    public int TvdbMovieID { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Overview { get; set; } = string.Empty;

    public DateOnly? ReleasedAt { get; set; }

    public int? RuntimeMinutes { get; set; }

    public string Status { get; set; } = string.Empty;

    public string OriginalLanguage { get; set; } = string.Empty;

    public string OriginalCountry { get; set; } = string.Empty;

    public List<string> Genres { get; set; } = [];

    public double UserRating { get; set; }

    public int? Year { get; set; }

    public string? PosterPath { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime LastUpdatedAt { get; set; }

    public TVDB_Movie() { }

    public TVDB_Movie(int tvdbMovieId)
    {
        TvdbMovieID = tvdbMovieId;
        CreatedAt = DateTime.Now;
        LastUpdatedAt = CreatedAt;
    }

    public bool Populate(JsonElement data)
    {
        var name = GetString(data, "name") ?? Name;
        var overview = GetString(data, "overview") ?? Overview;
        var released = ParseDate(GetString(data, "released") ?? GetString(data, "releaseDate"));
        var runtime = GetInt(data, "runtime");
        var status = GetNestedString(data, "status", "name") ?? Status;
        var originalLang = GetString(data, "originalLanguage") ?? OriginalLanguage;
        var originalCountry = GetString(data, "originalCountry") ?? OriginalCountry;
        var genres = GetGenres(data);
        var rating = GetDouble(data, "score") / 10000.0;
        var year = GetInt(data, "year");
        var poster = GetArtworkUrl(data);

        var updated = false;
        updated |= SetStr(Name, name, v => Name = v);
        updated |= SetStr(Overview, overview, v => Overview = v);
        updated |= SetStr(Status, status, v => Status = v);
        updated |= SetStr(OriginalLanguage, originalLang, v => OriginalLanguage = v);
        updated |= SetStr(OriginalCountry, originalCountry, v => OriginalCountry = v);
        if (ReleasedAt != released) { ReleasedAt = released; updated = true; }
        if (RuntimeMinutes != runtime) { RuntimeMinutes = runtime; updated = true; }
        if (string.Join("|", Genres) != string.Join("|", genres)) { Genres = genres; updated = true; }
        if (Math.Abs(UserRating - rating) > 0.001) { UserRating = rating; updated = true; }
        if (Year != year) { Year = year; updated = true; }
        if (PosterPath != poster) { PosterPath = poster; updated = true; }
        if (updated)
            LastUpdatedAt = DateTime.Now;
        return updated;
    }

    private static bool SetStr(string current, string value, Action<string> setter)
    {
        if (current == value)
            return false;
        setter(value);
        return true;
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

    private static double GetDouble(JsonElement el, string key)
    {
        if (el.TryGetProperty(key, out var prop))
        {
            if (prop.ValueKind is JsonValueKind.Number && prop.TryGetDouble(out var d))
                return d;
            if (prop.ValueKind is JsonValueKind.String && double.TryParse(prop.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out d))
                return d;
        }
        return 0;
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

    private static List<string> GetGenres(JsonElement data)
    {
        if (!data.TryGetProperty("genres", out var arr) || arr.ValueKind is not JsonValueKind.Array)
            return [];
        return arr.EnumerateArray()
            .Select(g => GetString(g, "name") ?? string.Empty)
            .Where(n => n.Length > 0)
            .ToList();
    }

    private static string? GetArtworkUrl(JsonElement data)
    {
        if (data.TryGetProperty("image", out var imgProp) && imgProp.ValueKind is JsonValueKind.String)
            return imgProp.GetString();
        if (data.TryGetProperty("artworks", out var artworks) && artworks.ValueKind is JsonValueKind.Array)
        {
            foreach (var art in artworks.EnumerateArray())
            {
                var type = GetInt(art, "type");
                if (type is 1 or 15)
                    return GetString(art, "image");
            }
        }
        return null;
    }
}
