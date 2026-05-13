using System;
using DaCollector.Server.Models.DaCollector;
using DaCollector.Server.Models.TMDB;
using DaCollector.Server.Repositories;

#nullable enable
namespace DaCollector.Server.Models.CrossReference;

public class CrossRef_File_TmdbEpisode
{
    public int CrossRef_File_TmdbEpisodeID { get; set; }

    public int VideoLocalID { get; set; }

    public int TmdbEpisodeID { get; set; }

    public int Percentage { get; set; } = 100;

    public int EpisodeOrder { get; set; } = 1;

    public bool IsManuallyLinked { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public VideoLocal? VideoLocal => RepoFactory.VideoLocal.GetByID(VideoLocalID);

    public TMDB_Episode? TmdbEpisode => RepoFactory.TMDB_Episode.GetByTmdbEpisodeID(TmdbEpisodeID);
}
