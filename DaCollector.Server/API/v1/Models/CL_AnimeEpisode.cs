using System;

namespace DaCollector.Server.API.v1.Models;

public class CL_AnimeEpisode
{
    public int MediaEpisodeID { get; set; }
    public int MediaSeriesID { get; set; }
    public int AniDB_EpisodeID { get; set; }
    public DateTime DateTimeCreated { get; set; }
    public DateTime DateTimeUpdated { get; set; }
}
