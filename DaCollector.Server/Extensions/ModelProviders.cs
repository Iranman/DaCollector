using System;
using DaCollector.Server.Models.AniDB;
using DaCollector.Server.Models.DaCollector;

namespace DaCollector.Server.Extensions;

public static class ModelProviders
{
    public static void Populate(this MediaGroup group, MediaSeries series, DateTime now)
    {
        group.Description = series.PreferredOverview?.Value ?? string.Empty;
        var name = series.Title;
        group.GroupName = name;
        group.MainAniDBAnimeID = series.AniDB_ID;
        group.DateTimeUpdated = now;
        group.DateTimeCreated = now;
    }

    public static void Populate(this MediaGroup group, AniDB_Anime anime, DateTime now)
    {
        group.Description = anime.Description;
        var name = anime.Title;
        group.GroupName = name;
        group.MainAniDBAnimeID = anime.AnimeID;
        group.DateTimeUpdated = now;
        group.DateTimeCreated = now;
    }
}
