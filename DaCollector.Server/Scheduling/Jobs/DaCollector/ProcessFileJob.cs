using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartz;
using DaCollector.Abstractions.Video.Services;
using DaCollector.Server.Media;
using DaCollector.Server.Models.DaCollector;
using DaCollector.Server.Repositories;
using DaCollector.Server.Scheduling.Acquisition.Attributes;
using DaCollector.Server.Scheduling.Attributes;
using DaCollector.Server.Scheduling.Concurrency;
using DaCollector.Server.Utilities;

#pragma warning disable CS8618
#nullable enable
namespace DaCollector.Server.Scheduling.Jobs.DaCollector;

[DatabaseRequired]
[LimitConcurrency(4)]
[AniDBUdpRateLimited]
[DisallowConcurrencyGroup(ConcurrencyGroups.AniDB_UDP)]
[JobKeyGroup(JobKeyGroup.Import)]
public class ProcessFileJob : BaseJob
{
    private readonly IVideoReleaseService _videoReleaseService;

    private readonly IVideoRelocationService _relocationService;

    private readonly MediaFileMatchCandidateService _candidateService;

    private VideoLocal _vlocal;

    private string _fileName;

    public int VideoLocalID { get; set; }

    public bool ForceRecheck { get; set; }

    public bool SkipMyList { get; set; }

    public bool ShouldRelocate { get; set; }

    public override string TypeName => "Get Release Information for Video";

    public override string Title => "Getting Release Information for Video";

    public override Dictionary<string, object> Details
    {
        get
        {
            var result = new Dictionary<string, object> { };
            if (string.IsNullOrEmpty(_fileName))
                result["Video"] = VideoLocalID;
            else
                result["File Path"] = _fileName;
            if (ForceRecheck) result["Force"] = true;
            if (!SkipMyList) result["Add to MyList"] = true;
            return result;
        }
    }

    public override void PostInit()
    {
        _vlocal = RepoFactory.VideoLocal.GetByID(VideoLocalID);
        if (_vlocal == null) throw new JobExecutionException($"VideoLocal not Found: {VideoLocalID}");
        _fileName = Utils.GetDistinctPath(_vlocal?.FirstValidPlace?.Path);
    }

    public override async Task Process()
    {
        _logger.LogInformation("Processing {Job}: {FileName}", nameof(ProcessFileJob), _fileName ?? VideoLocalID.ToString());

        // Check if the video local (file) is available.
        if (_vlocal == null)
        {
            _vlocal = RepoFactory.VideoLocal.GetByID(VideoLocalID);
            if (_vlocal == null)
                return;
        }

        // Try the legacy release-provider path (AniDB, etc.).
        var hadExistingRelease = !ForceRecheck && _videoReleaseService.GetCurrentReleaseForVideo(_vlocal) is not null;
        if (!hadExistingRelease)
        {
            var found = await _videoReleaseService.FindReleaseForVideo(_vlocal, addToMylist: !SkipMyList, isAutomatic: true);
            // If the release provider matched, the file is handled by the legacy pipeline.
            if (found is not null)
                hadExistingRelease = true;
        }

        if (ShouldRelocate)
            await _relocationService.ScheduleAutoRelocationForVideo(_vlocal);

        // For files with no release match, run the filename-parser / candidate-scan path so they
        // appear in the unmatched review queue with parsed metadata and provider candidates.
        if (!hadExistingRelease)
            await _candidateService.ScanFileAsync(VideoLocalID, refreshExplicitIds: true);
    }


    public ProcessFileJob(IVideoReleaseService videoReleaseService, IVideoRelocationService relocationService, MediaFileMatchCandidateService candidateService)
    {
        _videoReleaseService = videoReleaseService;
        _relocationService = relocationService;
        _candidateService = candidateService;
    }

    protected ProcessFileJob() { }
}
