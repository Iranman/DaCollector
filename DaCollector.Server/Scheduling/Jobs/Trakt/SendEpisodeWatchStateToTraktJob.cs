using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using DaCollector.Server.Models.AniDB;
using DaCollector.Server.Providers.AniDB;
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
public class SendEpisodeWatchStateToTraktJob : BaseJob
{
    private readonly ISettingsProvider _settingsProvider;
    private readonly TraktTVHelper _helper;
    private AniDB_Episode _episode;
    public int AnimeEpisodeID { get; set; }
    public TraktSyncType Action { get; set; }

    public override string TypeName => "Send Episode Watch State to Trakt";
    public override string Title => "Sending Episode Watch State to Trakt";
    public override void PostInit()
    {
        _episode = RepoFactory.AnimeEpisode?.GetByID(AnimeEpisodeID)?.AniDB_Episode;
    }

    public override Dictionary<string, object> Details =>
        _episode == null ? new()
        {
            { "EpisodeID", AnimeEpisodeID }
        } : new()
        {
            { "Anime", RepoFactory.AniDB_Anime.GetByAnimeID(_episode.AnimeID)?.PreferredTitle },
            { "Episode Type", ((EpisodeType)_episode.EpisodeType).ToString() },
            { "Episode Number", _episode.EpisodeNumber },
            { "Sync Action", Action.ToString() }
        };

    public override Task Process()
    {
        _logger.LogInformation("Processing {Job}", nameof(SendEpisodeWatchStateToTraktJob));
        var settings = _settingsProvider.GetSettings();

        if (!settings.TraktTv.Enabled || string.IsNullOrEmpty(settings.TraktTv.AuthToken)) return Task.CompletedTask;

        var episode = RepoFactory.AnimeEpisode.GetByID(AnimeEpisodeID);
        if (episode == null) return Task.CompletedTask;

        _helper.SendEpisodeWatchState(Action, episode);

        return Task.CompletedTask;
    }

    public SendEpisodeWatchStateToTraktJob(TraktTVHelper helper, ISettingsProvider settingsProvider)
    {
        _helper = helper;
        _settingsProvider = settingsProvider;
    }

    protected SendEpisodeWatchStateToTraktJob() { }
}
