using System;

# nullable enable
namespace DaCollector.Server.Models.AniDB;

public class AniDB_AnimeUpdate
{
    public int AniDB_AnimeUpdateID { get; set; }

    public int AnimeID { get; set; }

    public DateTime UpdatedAt { get; set; }
}
