using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using DaCollector.Server.Providers.TraktTV;
using DaCollector.Server.Repositories;
using DaCollector.Server.Scheduling.Acquisition.Attributes;
using DaCollector.Server.Scheduling.Attributes;
using DaCollector.Server.Scheduling.Concurrency;
using DaCollector.Server.Settings;

namespace DaCollector.Server.Scheduling.Jobs.Trakt;

[DatabaseRequired]
[NetworkRequired]
[DisallowConcurrencyGroup(ConcurrencyGroups.Trakt)]
[JobKeyGroup(JobKeyGroup.Trakt)]
public class SendSeriesWatchStatesToTraktJob : BaseJob
{
    private readonly ISettingsProvider _settingsProvider;
    private readonly TraktTVHelper _helper;
    private string _seriesName;
    public int MediaSeriesID { get; set; }

    public override string TypeName => "Send Series Watch States to Trakt";
    public override string Title => "Sending Series Watch States to Trakt";

    public override void PostInit()
    {
        _seriesName = RepoFactory.MediaSeries?.GetByID(MediaSeriesID)?.Title ?? MediaSeriesID.ToString();
    }

    public override Dictionary<string, object> Details => new() { { "Anime", _seriesName } };

    public override Task Process()
    {
        _logger.LogInformation("Processing {Job} -> Series: {Name}", nameof(SendSeriesWatchStatesToTraktJob), _seriesName);
        var settings = _settingsProvider.GetSettings();
        if (!settings.TraktTv.Enabled || string.IsNullOrEmpty(settings.TraktTv.AuthToken)) return Task.CompletedTask;

        var series = RepoFactory.MediaSeries.GetByID(MediaSeriesID);
        if (series == null)
        {
            _logger.LogError("Could not find anime series: {MediaSeriesID}", MediaSeriesID);
            return Task.CompletedTask;
        }

        _helper.SendWatchStates(series);

        return Task.CompletedTask;
    }

    public SendSeriesWatchStatesToTraktJob(TraktTVHelper helper, ISettingsProvider settingsProvider)
    {
        _helper = helper;
        _settingsProvider = settingsProvider;
    }

    protected SendSeriesWatchStatesToTraktJob() { }
}
