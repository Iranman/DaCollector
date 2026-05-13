using System;
using DaCollector.Server.Models.DaCollector;
using DaCollector.Server.Models.TVDB;
using DaCollector.Server.Repositories;

#nullable enable
namespace DaCollector.Server.Models.CrossReference;

public class CrossRef_File_TvdbEpisode
{
    public int CrossRef_File_TvdbEpisodeID { get; set; }

    public int VideoLocalID { get; set; }

    public int TvdbEpisodeID { get; set; }

    public int Percentage { get; set; } = 100;

    public int EpisodeOrder { get; set; } = 1;

    public bool IsManuallyLinked { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public VideoLocal? VideoLocal => RepoFactory.VideoLocal.GetByID(VideoLocalID);

    public TVDB_Episode? TvdbEpisode => RepoFactory.TVDB_Episode.GetByTvdbEpisodeID(TvdbEpisodeID);
}
