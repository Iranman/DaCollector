using System.Collections.Generic;

namespace DaCollector.Server.API.v1.Models;

public class CL_AnimeSeries_FileStats
{
    public int MediaSeriesID { get; set; }
    public string AnimeSeriesName { get; set; }
    public List<string> Folders { get; set; }
    public int FileCount { get; set; }
    public long FileSize { get; set; }
}
