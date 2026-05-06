using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartz;
using DaCollector.Abstractions.Video.Services;
using DaCollector.Server.Models.DaCollector;
using DaCollector.Server.Repositories;
using DaCollector.Server.Scheduling.Acquisition.Attributes;
using DaCollector.Server.Scheduling.Attributes;
using DaCollector.Server.Scheduling.Concurrency;
using DaCollector.Server.Services;
using DaCollector.Server.Utilities;

namespace DaCollector.Server.Scheduling.Jobs.DaCollector;

[DatabaseRequired]
[LimitConcurrency(2)]
[JobKeyGroup(JobKeyGroup.Import)]
public class MediaInfoJob : BaseJob
{
    private readonly VideoService _videoService;

    private VideoLocal _vlocal;
    private string _fileName;

    public int VideoLocalID { get; set; }

    public override string TypeName => "Read MediaInfo for File";
    public override string Title => "Reading MediaInfo for File";

    public override void PostInit()
    {
        _vlocal = RepoFactory.VideoLocal.GetByID(VideoLocalID);
        if (_vlocal == null) throw new JobExecutionException($"VideoLocal not Found: {VideoLocalID}");
        _fileName = Utils.GetDistinctPath(_vlocal.FirstValidPlace?.Path);
    }

    public override Dictionary<string, object> Details => new() { { "File Path", _fileName ?? VideoLocalID.ToString() } };

    public override Task Process()
    {
        _logger.LogInformation("Processing {Job}: {FileName}", nameof(MediaInfoJob), _fileName);

        var place = _vlocal?.FirstResolvedPlace;
        if (place == null)
        {
            _logger.LogWarning("Could not find file for Video: {VideoLocalID}", VideoLocalID);
            return Task.CompletedTask;
        }

        if (_videoService.RefreshMediaInfo(place, _vlocal))
        {
            RepoFactory.VideoLocal.Save(place.VideoLocal, true);
        }

        return Task.CompletedTask;
    }

    public MediaInfoJob(IVideoService videoService)
    {
        _videoService = (VideoService)videoService;
    }

    protected MediaInfoJob() { }
}
