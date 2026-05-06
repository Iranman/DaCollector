using System.Collections.Generic;
using DaCollector.Abstractions.Metadata.Enums;
using DaCollector.Server.Repositories;
using DaCollector.Server.Scheduling.Acquisition.Attributes;
using DaCollector.Server.Scheduling.Attributes;
using DaCollector.Server.Scheduling.Concurrency;

namespace DaCollector.Server.Scheduling.Jobs.TMDB;

[DatabaseRequired]
[NetworkRequired]
[LimitConcurrency(12, 24)]
[JobKeyGroup(JobKeyGroup.TMDB)]
public class DownloadTmdbImageJob : DownloadImageBaseJob
{
    public override DataSource Source => DataSource.TMDB;

    public override Dictionary<string, object> Details => new()
    {
        { "Type", ImageType.ToString().Replace("_", " ") },
        { "ImageID", ImageID }
    };

    public DownloadTmdbImageJob() : base() { }

    protected override bool RemoveRecord()
    {
        if (RepoFactory.TMDB_Image.GetByID(ImageID) is { } image)
            RepoFactory.TMDB_Image.Delete(image);
        return true;
    }
}
