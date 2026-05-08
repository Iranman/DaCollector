using System;
using System.Collections.Generic;
using DaCollector.Abstractions.Collections;
using DaCollector.Abstractions.Metadata.Enums;

namespace DaCollector.Server.Collections;

/// <summary>
/// Initial catalog of provider-driven collection builders being ported into DaCollector.
/// </summary>
public static class CollectionBuilderCatalog
{
    public static IReadOnlyDictionary<string, CollectionBuilderDescriptor> All { get; } = new Dictionary<string, CollectionBuilderDescriptor>(StringComparer.OrdinalIgnoreCase)
    {
        ["tmdb_movie"] = Tmdb("tmdb_movie", MediaKind.Movie, "Add a specific TMDB movie."),
        ["tmdb_show"] = Tmdb("tmdb_show", MediaKind.Show, "Add a specific TMDB show."),
        ["tmdb_collection"] = Tmdb("tmdb_collection", MediaKind.Collection, "Add movies from a TMDB movie collection."),
        ["tmdb_popular"] = Tmdb("tmdb_popular", MediaKind.Unknown, "Add popular TMDB movies or shows."),
        ["tmdb_top_rated"] = Tmdb("tmdb_top_rated", MediaKind.Unknown, "Add top-rated TMDB movies or shows."),
        ["tmdb_trending_daily"] = Tmdb("tmdb_trending_daily", MediaKind.Unknown, "Add daily trending TMDB movies or shows."),
        ["tmdb_trending_weekly"] = Tmdb("tmdb_trending_weekly", MediaKind.Unknown, "Add weekly trending TMDB movies or shows."),
        ["tmdb_discover"] = Tmdb("tmdb_discover", MediaKind.Unknown, "Add TMDB movies or shows from discover filters."),
        ["tmdb_now_playing"] = Tmdb("tmdb_now_playing", MediaKind.Movie, "Add TMDB movies currently in theaters."),
        ["tmdb_upcoming"] = Tmdb("tmdb_upcoming", MediaKind.Movie, "Add upcoming TMDB movies."),
        ["tmdb_airing_today"] = Tmdb("tmdb_airing_today", MediaKind.Show, "Add TMDB shows airing today."),
        ["tmdb_on_the_air"] = Tmdb("tmdb_on_the_air", MediaKind.Show, "Add TMDB shows currently on the air."),

        ["tvdb_movie"] = Tvdb("tvdb_movie", MediaKind.Movie, "Add a specific TVDB movie."),
        ["tvdb_show"] = Tvdb("tvdb_show", MediaKind.Show, "Add a specific TVDB show."),
        ["tvdb_list"] = Tvdb("tvdb_list", MediaKind.Unknown, "Add titles from a TVDB list."),
    };

    public static bool TryGet(string name, out CollectionBuilderDescriptor descriptor)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            descriptor = new CollectionBuilderDescriptor();
            return false;
        }

        return All.TryGetValue(name.Trim(), out descriptor);
    }

    private static CollectionBuilderDescriptor Tmdb(string name, MediaKind kind, string description) =>
        Create(name, ExternalProvider.TMDB, kind, description);

    private static CollectionBuilderDescriptor Tvdb(string name, MediaKind kind, string description) =>
        Create(name, ExternalProvider.TVDB, kind, description);

    private static CollectionBuilderDescriptor Create(string name, ExternalProvider provider, MediaKind kind, string description) =>
        new()
        {
            Name = name,
            Provider = provider,
            Kind = kind,
            Description = description,
        };
}
