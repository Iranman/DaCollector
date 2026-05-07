using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using DaCollector.Abstractions.Extensions;
using DaCollector.Server.Providers.AniDB;
using DaCollector.Server.Providers.AniDB.Interfaces;
using DaCollector.Server.Providers.AniDB.Release;
using DaCollector.Server.Providers.AniDB.UDP.User;
using DaCollector.Server.Repositories;
using DaCollector.Server.Scheduling.Acquisition.Attributes;
using DaCollector.Server.Scheduling.Attributes;
using DaCollector.Server.Scheduling.Concurrency;
using DaCollector.Server.Services;
using DaCollector.Server.Settings;

namespace DaCollector.Server.Scheduling.Jobs.AniDB;

[DatabaseRequired]
[AniDBUdpRateLimited]
[DisallowConcurrencyGroup(ConcurrencyGroups.AniDB_UDP)]
[JobKeyGroup(JobKeyGroup.AniDB)]
public class UpdateMyListFileStatusJob : BaseJob
{
    private readonly IRequestFactory _requestFactory;
    private readonly ISettingsProvider _settingsProvider;
    private readonly MediaSeriesService _seriesService;

    private string FullFileName { get; set; }
    public string Hash { get; set; }
    public bool? Watched { get; set; }
    public bool UpdateSeriesStats { get; set; }
    public DateTime? WatchedDate { get; set; }

    public override string TypeName => "Update AniDB MyList Status for File";
    public override string Title => "Updating AniDB MyList Status for File";

    public override void PostInit()
    {
        FullFileName = RepoFactory.FileNameHash?.GetByHash(Hash).FirstOrDefault()?.FileName;
    }

    public override Dictionary<string, object> Details => FullFileName != null ? new()
    {
        { "Filename", FullFileName},
        { "Watched", Watched },
        { "Date", WatchedDate }
    } : new()
    {
        { "Hash", Hash },
        { "Watched", Watched },
        { "Date", WatchedDate }
    };

    public override async Task Process()
    {
        _logger.LogInformation("Processing {Job} for {Filename} | {Watched} | {WatchedDate}", nameof(UpdateMyListFileStatusJob), FullFileName, Watched, WatchedDate);

        var settings = _settingsProvider.GetSettings();
        // NOTE - we might return more than one VideoLocal record here, if there are duplicates by hash
        var vid = RepoFactory.VideoLocal.GetByEd2k(Hash);
        if (vid == null) return;

        if (vid.ReleaseInfo is { } releaseInfo && (releaseInfo.ReleaseURI?.StartsWith(AnidbReleaseProvider.ReleasePrefix) ?? false))
        {
            _logger.LogInformation("Updating File MyList Status: {Hash}|{Size}", vid.Hash, vid.FileSize);
            var request = _requestFactory.Create<RequestUpdateFile>(
                r =>
                {
                    r.State = settings.AniDb.MyList_StorageState;
                    r.Hash = vid.Hash;
                    r.Size = vid.FileSize;
                    r.IsWatched = Watched;
                    r.WatchedDate = WatchedDate;
                }
            );

            request.Send();
        }
        else
        {
            // we have a manual link, so get the xrefs and add the episodes instead as generic files
            var xrefs = vid.EpisodeCrossReferences;
            foreach (var episode in xrefs.Select(xref => xref.AniDBEpisode).WhereNotNull())
            {
                _logger.LogInformation("Updating Episode MyList Status: AnimeID: {AnimeID}, Episode Type: {Type}, Episode No: {EP}", episode.AnimeID,
                    episode.EpisodeType, episode.EpisodeNumber);
                var request = _requestFactory.Create<RequestUpdateEpisode>(
                    r =>
                    {
                        r.State = settings.AniDb.MyList_StorageState;
                        r.AnimeID = episode.AnimeID;
                        r.EpisodeNumber = episode.EpisodeNumber;
                        r.EpisodeType = (EpisodeType)episode.EpisodeType;
                        r.IsWatched = Watched;
                        r.WatchedDate = WatchedDate;
                    }
                );

                request.Send();
            }
        }

        if (!UpdateSeriesStats) return;

        // update watched stats
        var eps = RepoFactory.MediaEpisode.GetByHash(vid.Hash);
        if (eps.Count > 0) await Task.WhenAll(eps.DistinctBy(a => a.MediaSeriesID).Select(a => _seriesService.QueueUpdateStats(a.MediaSeries)));
    }

    public UpdateMyListFileStatusJob(IRequestFactory requestFactory, ISettingsProvider settingsProvider, MediaSeriesService seriesService)
    {
        _requestFactory = requestFactory;
        _settingsProvider = settingsProvider;
        _seriesService = seriesService;
    }

    protected UpdateMyListFileStatusJob() { }
}
