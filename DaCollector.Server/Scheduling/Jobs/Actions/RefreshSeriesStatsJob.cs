using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using DaCollector.Server.Repositories.Cached;
using DaCollector.Server.Scheduling.Acquisition.Attributes;
using DaCollector.Server.Scheduling.Attributes;
using DaCollector.Server.Services;

#pragma warning disable CS8618
#nullable enable
namespace DaCollector.Server.Scheduling.Jobs.Actions;

[DatabaseRequired]
[JobKeyGroup(JobKeyGroup.Actions)]
public class RefreshSeriesStatsJob : BaseJob
{
    private readonly MediaSeriesRepository _seriesRepo;
    private readonly MediaSeriesService _seriesService;
    private readonly MediaGroupService _groupService;

    public int MediaSeriesID { get; set; }

    private string? _seriesTitle;

    public override string TypeName => "Refresh Series Stats";
    public override string Title => "Refreshing Series Stats";
    public override Dictionary<string, object> Details => new() { { "MediaSeriesID", MediaSeriesID } };

    public override void PostInit()
    {
        _seriesTitle = _seriesRepo.GetByID(MediaSeriesID)?.Title ?? MediaSeriesID.ToString();
    }

    public override Task Process()
    {
        _logger.LogInformation("Processing {Job} for {Series}", nameof(RefreshSeriesStatsJob), _seriesTitle);
        var series = _seriesRepo.GetByID(MediaSeriesID);
        if (series is null)
        {
            _logger.LogWarning("MediaSeries not found: {MediaSeriesID}", MediaSeriesID);
            return Task.CompletedTask;
        }

        series.ResetAnimeTitles();
        series.ResetPreferredTitle();
        series.ResetPreferredOverview();

        _seriesService.UpdateStats(series, true, true);
        _groupService.UpdateStatsFromTopLevel(series.MediaGroup?.TopLevelMediaGroup, true, true);
        return Task.CompletedTask;
    }

    public RefreshSeriesStatsJob(MediaSeriesService seriesService, MediaSeriesRepository seriesRepo, MediaGroupService groupService)
    {
        _seriesService = seriesService;
        _seriesRepo = seriesRepo;
        _groupService = groupService;
    }

    protected RefreshSeriesStatsJob() { }
}
