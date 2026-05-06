using Quartz;
using DaCollector.Abstractions.Metadata.Enums;

#nullable enable
namespace DaCollector.Server.Scheduling.Jobs;

public interface IImageDownloadJob : IJob
{
    string? ParentName { get; set; }

    bool ForceDownload { get; set; }

    int ImageID { get; set; }

    ImageEntityType ImageType { get; set; }
}
