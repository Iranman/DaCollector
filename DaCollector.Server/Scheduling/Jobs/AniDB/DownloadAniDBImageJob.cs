using System.Collections.Generic;
using DaCollector.Abstractions.Metadata.Enums;
using DaCollector.Server.Scheduling.Acquisition.Attributes;
using DaCollector.Server.Scheduling.Attributes;
using DaCollector.Server.Scheduling.Concurrency;

namespace DaCollector.Server.Scheduling.Jobs.AniDB;

[DatabaseRequired]
[NetworkRequired]
[LimitConcurrency(8, 16)]
[JobKeyGroup(JobKeyGroup.AniDB)]
public class DownloadAniDBImageJob : DownloadImageBaseJob
{
    public override DataSource Source => DataSource.AniDB;

    public override Dictionary<string, object> Details => ImageType switch
    {
        ImageEntityType.Poster when ParentName is not null => new()
        {
            { "Anime", ParentName },
            { "Type", "AniDB Poster" },
        },
        ImageEntityType.Poster when ParentName is null => new()
        {
            { "Anime", $"AniDB Anime {ImageID}" },
            { "Type", "AniDB Poster" },
        },
        _ when ParentName is not null => new()
        {
            { "Anime", ParentName },
            { "Type", $"AniDB {ImageType}".Replace("Person", "Creator") },
            { "ImageID", ImageID }
        },
        _ => new()
        {
            { "Type", $"AniDB {ImageType}".Replace("Person", "Creator") },
            { "ImageID", ImageID }
        }
    };

    public DownloadAniDBImageJob() : base() { }
}
