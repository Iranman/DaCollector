using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using DaCollector.Abstractions.Extensions;
using DaCollector.Abstractions.User.Services;
using DaCollector.Server.Models.DaCollector;
using DaCollector.Server.Plex;
using DaCollector.Server.Plex.Collection;
using DaCollector.Server.Plex.Libraries;
using DaCollector.Server.Plex.TVShow;
using DaCollector.Server.Repositories.Cached;
using DaCollector.Server.Scheduling.Acquisition.Attributes;
using DaCollector.Server.Scheduling.Attributes;
using DaCollector.Server.Scheduling.Concurrency;
using DaCollector.Server.Settings;

namespace DaCollector.Server.Scheduling.Jobs.Plex;

[DatabaseRequired]
[NetworkRequired]
[LimitConcurrency(1, 1)]
[JobKeyGroup(JobKeyGroup.Actions)]
public class SyncPlexWatchedStatesJob : BaseJob
{
    private readonly ISettingsProvider _settingsProvider;
    private readonly VideoLocal_UserRepository _vlUsers;
    private readonly IUserDataService _userDataService;
    public JMMUser User { get; set; }

    public override string TypeName => "Sync Plex States for User";

    public override string Title => "Syncing Plex States for User";
    public override Dictionary<string, object> Details => new()
    {
        { "User", User.Username }
    };

    public override async Task Process()
    {
        _logger.LogInformation("Processing {Job} -> User: {Name}", nameof(SyncPlexWatchedStatesJob), User.Username);
        var settings = _settingsProvider.GetSettings();
        foreach (var section in PlexHelper.GetForUser(User).GetDirectories().Where(a => settings.Plex.Libraries.Contains(a.Key)))
        {
            var allSeries = ((SVR_Directory)section).GetShows();
            foreach (var series in allSeries)
            {
                var episodes = ((SVR_PlexLibrary)series)?.GetEpisodes()?.WhereNotNull();
                if (episodes == null) continue;

                foreach (var ep in episodes)
                {
                    using var scope = _logger.BeginScope(ep.Key);
                    var episode = (SVR_Episode)ep;

                    var MediaEpisode = episode.MediaEpisode;


                    _logger.LogInformation("Processing episode {Title} of {SeriesName}", episode.Title, series.Title);
                    if (MediaEpisode == null)
                    {
                        var filePath = episode.Media[0].Part[0].File;
                        _logger.LogTrace("Episode not found in DaCollector, skipping - {Filename} ({FilePath})", Path.GetFileName(filePath), filePath);
                        continue;
                    }

                    var userRecord = MediaEpisode.GetUserRecord(User.JMMUserID);
                    var isWatched = episode.ViewCount is > 0;
                    var lastWatched = userRecord?.WatchedDate;
                    if ((userRecord?.WatchedCount ?? 0) == 0 && isWatched && episode.LastViewedAt != null)
                    {
                        lastWatched = FromUnixTime((long)episode.LastViewedAt);
                        _logger.LogTrace("Last watched date is {LastWatched}", lastWatched);
                    }

                    var video = MediaEpisode.VideoLocals?.FirstOrDefault();
                    if (video == null) continue;

                    var alreadyWatched = MediaEpisode.VideoLocals
                        .Select(a => _vlUsers.GetByUserAndVideoLocalID(User.JMMUserID, a.VideoLocalID))
                        .WhereNotNull()
                        .Any(x => x.WatchedDate is not null || x.WatchedCount > 0);

                    if (!alreadyWatched && userRecord != null)
                    {
                        alreadyWatched = userRecord.IsWatched;
                    }

                    _logger.LogTrace("Already watched in dacollector? {AlreadyWatched} Has been watched in plex? {IsWatched}", alreadyWatched, isWatched);

                    if (alreadyWatched && !isWatched)
                    {
                        _logger.LogInformation("Marking episode watched in plex");
                        episode.Scrobble();
                    }

                    if (isWatched && !alreadyWatched)
                    {
                        _logger.LogInformation("Marking episode watched in DaCollector");
                        await _userDataService.SaveVideoUserData(video, User, new() { LastPlayedAt = lastWatched ?? DateTime.Now });
                    }
                }
            }
        }
    }

    private DateTime FromUnixTime(long unixTime)
    {
        return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            .AddSeconds(unixTime);
    }

    public SyncPlexWatchedStatesJob(ISettingsProvider settingsProvider, VideoLocal_UserRepository vlUsers, IUserDataService userDataService)
    {
        _settingsProvider = settingsProvider;
        _vlUsers = vlUsers;
        _userDataService = userDataService;
    }

    protected SyncPlexWatchedStatesJob() { }
}
