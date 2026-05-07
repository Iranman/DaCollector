using System.Collections.Generic;

namespace DaCollector.Server.API.v1.Models;

public class CL_AnimeSearch
{
    public int AnimeID { get; set; }
    public string MainTitle { get; set; }
    public HashSet<string> Titles { get; set; }
    public bool SeriesExists { get; set; }
    public int? MediaSeriesID { get; set; }
    public string AnimeSeriesName { get; set; }
    public int? MediaGroupID { get; set; }
    public string AnimeGroupName { get; set; }

    public override string ToString()
    {
        return $"{AnimeID} - {MainTitle} - {Titles}";
    }
}
