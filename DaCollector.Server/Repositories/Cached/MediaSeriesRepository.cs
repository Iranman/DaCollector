using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NHibernate;
using NLog;
using NutzCode.InMemoryIndex;
using DaCollector.Abstractions.Metadata.Enums;
using DaCollector.Abstractions.Extensions;
using DaCollector.Server.Databases;
using DaCollector.Server.Models.AniDB;
using DaCollector.Server.Models.DaCollector;
using DaCollector.Server.Repositories.NHibernate;
using DaCollector.Server.Tasks;
using DaCollector.Server.Utilities;

#pragma warning disable CA1822
#nullable enable
namespace DaCollector.Server.Repositories.Cached;

public class MediaSeriesRepository : BaseCachedRepository<MediaSeries, int>
{
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();

    private PocoIndex<int, MediaSeries, int>? AniDBIds;
    private PocoIndex<int, MediaSeries, int>? Groups;

    private readonly ChangeTracker<int> Changes = new();

    public MediaSeriesRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
        BeginDeleteCallback = cr =>
        {
            RepoFactory.MediaSeries_User.Delete(RepoFactory.MediaSeries_User.GetBySeriesID(cr.MediaSeriesID));
            Changes.Remove(cr.MediaSeriesID);
        };
        EndDeleteCallback = cr =>
        {
            if (cr.MediaGroupID <= 0)
            {
                return;
            }

            logger.Trace("Updating group stats by group from MediaSeriesRepository.Delete: {0}",
                cr.MediaGroupID);
            var oldGroup = RepoFactory.MediaGroup.GetByID(cr.MediaGroupID);
            if (oldGroup != null)
            {
                RepoFactory.MediaGroup.Save(oldGroup, true);
            }
        };
    }

    protected override int SelectKey(MediaSeries entity)
    {
        return entity.MediaSeriesID;
    }

    public override void PopulateIndexes()
    {
        Changes.AddOrUpdateRange(Cache.Keys);
        AniDBIds = Cache.CreateIndex(a => a.AniDB_ID);
        Groups = Cache.CreateIndex(a => a.MediaGroupID);
    }

    public override void RegenerateDb()
    {
        try
        {
            SystemService.StartupMessage =
                $"Database - Validating - {nameof(MediaSeries)} Database Regeneration - Caching Titles & Overview...";
            foreach (var series in Cache.Values.ToList())
            {
                series.ResetPreferredTitle();
                series.ResetPreferredOverview();
                series.ResetAnimeTitles();
            }

            var sers = Cache.Values.Where(a => a.MediaGroupID == 0 || RepoFactory.MediaGroup.GetByID(a.MediaGroupID) == null).ToList();
            var max = sers.Count;
            SystemService.StartupMessage = $"Database - Validating - {nameof(MediaSeries)} Database Regeneration - Ensuring Groups Exist...";

            var groupCreator = Utils.ServiceContainer.GetRequiredService<MediaGroupCreator>();
            for (var i = 0; i < max; i++)
            {
                var s = sers[i];
                try
                {
                    var group = groupCreator.GetOrCreateSingleGroupForSeries(s);
                    s.MediaGroupID = group.MediaGroupID;
                    Save(s, false, true);
                }
                catch
                {
                    // ignore
                }

                if (i % 10 != 0) continue;
                SystemService.StartupMessage =
                    $"Database - Validating - {nameof(MediaSeries)} DbRegen - Ensuring Groups Exist - {i}/{max}...";
            }

            SystemService.StartupMessage =
                $"Database - Validating - {nameof(MediaSeries)} DbRegen - Ensuring Groups Exist - {max}/{max}...";
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public ChangeTracker<int> GetChangeTracker()
    {
        return Changes;
    }

    public override void Save(MediaSeries obj)
    {
        Save(obj, false);
    }

    public void Save(MediaSeries obj, bool onlyupdatestats)
    {
        Save(obj, true, onlyupdatestats);
    }

    public void Save(MediaSeries obj, bool updateGroups, bool onlyupdatestats, bool alsoupdateepisodes = false)
    {
        var animeID = obj.AniDB_Anime?.MainTitle ?? obj.AniDB_ID.ToString();
        logger.Trace($"Saving Series {animeID}");
        var totalSw = Stopwatch.StartNew();
        var sw = Stopwatch.StartNew();
        var newSeries = false;
        MediaGroup? oldGroup = null;
        // Updated Now
        obj.DateTimeUpdated = DateTime.Now;
        var isMigrating = false;
        if (obj.MediaSeriesID == 0)
        {
            newSeries = true; // a new series
        }
        else
        {
            // get the old version from the DB
            logger.Trace($"Saving Series {animeID} | Waiting for Database Lock");
            var oldSeries = Lock(obj.MediaSeriesID, animeID, sw, (MediaSeriesID, id, s) =>
            {
                s.Stop();
                logger.Trace($"Saving Series {id} | Got Database Lock in {s.Elapsed.TotalSeconds:0.00###}s");
                s.Restart();
                using var session = _databaseFactory.SessionFactory.OpenSession();
                var series = session.Get<MediaSeries>(MediaSeriesID);
                s.Stop();
                logger.Trace($"Saving Series {id} | Got Series from Database in {s.Elapsed.TotalSeconds:0.00###}s");
                s.Restart();
                return series;
            });

            if (oldSeries != null)
            {
                // means we are moving series to a different group
                if (oldSeries.MediaGroupID != obj.MediaGroupID)
                {
                    logger.Trace($"Saving Series {animeID} | Group ID is different. Moving to new group");
                    oldGroup = RepoFactory.MediaGroup.GetByID(oldSeries.MediaGroupID);
                    var newGroup = RepoFactory.MediaGroup.GetByID(obj.MediaGroupID);
                    if (newGroup is { GroupName: "AAA Migrating Groups AAA" })
                    {
                        isMigrating = true;
                    }

                    newSeries = true;
                }
            }
            else
            {
                // should not happen, but if it does, recover
                newSeries = true;
                logger.Trace(
                    $"Saving Series {animeID} | Unable to get series from database, attempting to make new record");
            }
        }

        if (newSeries && !isMigrating)
        {
            sw.Stop();
            logger.Trace($"Saving Series {animeID} | New Series added. Need to save first to get an ID");
            sw.Restart();
            base.Save(obj);
            sw.Stop();
            logger.Trace($"Saving Series {animeID} | Saved new series in {sw.Elapsed.TotalSeconds:0.00###}s");
            sw.Restart();
        }

        var seasons = obj.AniDB_Anime?.YearlySeasons;
        if (seasons == null || !seasons.Any()) RegenerateSeasons(obj, sw, animeID);

        sw.Stop();
        logger.Trace($"Saving Series {animeID} | Saving Series to Database");
        sw.Restart();
        base.Save(obj);
        sw.Stop();
        logger.Trace($"Saving Series {animeID} | Saved Series to Database in {sw.Elapsed.TotalSeconds:0.00###}s");
        sw.Restart();

        if (updateGroups && !isMigrating) UpdateGroups(obj, animeID, sw, oldGroup!);

        Changes.AddOrUpdate(obj.MediaSeriesID);

        if (alsoupdateepisodes) UpdateEpisodes(obj, sw, animeID);

        sw.Stop();
        totalSw.Stop();
        logger.Trace($"Saving Series {animeID} | Finished Saving in {totalSw.Elapsed.TotalSeconds:0.00###}s");
    }

    private static void RegenerateSeasons(MediaSeries obj, Stopwatch sw, string animeID)
    {
        sw.Stop();
        logger.Trace(
            $"Saving Series {animeID} | AniDB_Anime Contract is invalid or Seasons not generated. Regenerating");
        sw.Restart();
        var anime = obj.AniDB_Anime;
        if (anime != null)
        {
            RepoFactory.AniDB_Anime.Save(anime);
        }

        sw.Stop();
        logger.Trace($"Saving Series {animeID} | Regenerated AniDB_Anime Contract in {sw.Elapsed.TotalSeconds:0.00###}s");
        sw.Restart();
    }

    private static void UpdateEpisodes(MediaSeries obj, Stopwatch sw, string animeID)
    {
        sw.Stop();
        logger.Trace($"Saving Series {animeID} | Updating Episodes");
        sw.Restart();
        var eps = RepoFactory.MediaEpisode.GetBySeriesID(obj.MediaSeriesID);
        RepoFactory.MediaEpisode.Save(eps);
        sw.Stop();
        logger.Trace($"Saving Series {animeID} | Updated Episodes in {sw.Elapsed.TotalSeconds:0.00###}s");
        sw.Restart();
    }

    private static void UpdateGroups(MediaSeries obj, string animeID, Stopwatch sw, MediaGroup oldGroup)
    {
        logger.Trace($"Saving Series {animeID} | Also Updating Group {obj.MediaGroupID}");
        var grp = RepoFactory.MediaGroup.GetByID(obj.MediaGroupID);
        if (grp != null)
        {
            RepoFactory.MediaGroup.Save(grp, true);
        }
        else
            logger.Trace($"Saving Series {animeID} | Group {obj.MediaGroupID} was not found. Not Updating");

        sw.Stop();
        logger.Trace($"Saving Series {animeID} | Updated Group {obj.MediaGroupID} in {sw.Elapsed.TotalSeconds:0.00###}s");
        sw.Restart();

        // Last ditch to make sure we aren't just updating the same group twice (shouldn't be)
        if (oldGroup != null && grp?.MediaGroupID != oldGroup.MediaGroupID)
        {
            logger.Trace($"Saving Series {animeID} | Also Updating previous group {oldGroup.MediaGroupID}");
            RepoFactory.MediaGroup.Save(oldGroup, true);
            sw.Stop();
            logger.Trace(
                $"Saving Series {animeID} | Updated old group {oldGroup.MediaGroupID} in {sw.Elapsed.TotalSeconds:0.00###}s");
            sw.Restart();
        }
    }

    public async Task UpdateBatch(ISessionWrapper session, IReadOnlyCollection<MediaSeries> seriesBatch)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(seriesBatch);

        if (seriesBatch.Count == 0)
        {
            return;
        }

        foreach (var series in seriesBatch)
        {
            await Lock(async () => await session.UpdateAsync(series));
            UpdateCache(series);
            Changes.AddOrUpdate(series.MediaSeriesID);
        }
    }

    public MediaSeries? GetByAnimeID(int id)
    {
        return ReadLock(() => AniDBIds!.GetOne(id));
    }

    public List<MediaSeries> GetByGroupID(int groupid)
    {
        return ReadLock(() => Groups!.GetMultiple(groupid));
    }

    public List<MediaSeries> GetWithMissingEpisodes()
    {
        return ReadLock(() => Cache.Values.Where(a => a.MissingEpisodeCountGroups > 0)
            .OrderByDescending(a => a.EpisodeAddedDate)
            .ToList());
    }

    public List<MediaSeries> GetMostRecentlyAdded(int maxResults, int userID)
    {
        var user = RepoFactory.JMMUser.GetByID(userID);
        return ReadLock(() => user == null
            ? Cache.Values.OrderByDescending(a => a.DateTimeCreated).Take(maxResults).ToList()
            : Cache.Values.Where(a => user.AllowedSeries(a)).OrderByDescending(a => a.DateTimeCreated).Take(maxResults)
                .ToList());
    }

    private const string MultipleReleasesIgnoreVariationsQuery =
        @"SELECT DISTINCT ani.AnimeID FROM VideoLocal AS vl JOIN CrossRef_File_Episode ani ON vl.Hash = ani.Hash WHERE vl.IsVariation = 0 AND vl.Hash != '' GROUP BY ani.AnimeID, ani.EpisodeID HAVING COUNT(ani.EpisodeID) > 1";
    private const string MultipleReleasesCountVariationsQuery =
        @"SELECT DISTINCT ani.AnimeID FROM VideoLocal AS vl JOIN CrossRef_File_Episode ani ON vl.Hash = ani.Hash WHERE vl.Hash != '' GROUP BY ani.AnimeID, ani.EpisodeID HAVING COUNT(ani.EpisodeID) > 1";

    public IEnumerable<MediaSeries> GetWithMultipleReleases(bool ignoreVariations)
    {
        var ids = Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();

            var query = ignoreVariations ? MultipleReleasesIgnoreVariationsQuery : MultipleReleasesCountVariationsQuery;
            return session.CreateSQLQuery(query)
                .AddScalar("AnimeID", NHibernateUtil.Int32)
                .List<int>();
        });

        return ids
            .Distinct()
            .Select(GetByAnimeID)
            .WhereNotNull();
    }

    private const string DuplicateFilesQuery = @"
SELECT DISTINCT
    ani.AnimeID
FROM
    (
        SELECT
            vl.FileSize,
            vl.Hash
        FROM
            VideoLocal AS vl
        WHERE
            VideoLocalID IN (
                SELECT
                    VideoLocalID
                FROM
                    VideoLocal_Place
                GROUP BY
                    VideoLocalID
                HAVING
                    COUNT(VideoLocal_Place_ID) > 1
            )
        AND
            vl.Hash != ''
    ) AS vlp_selected
INNER JOIN
    CrossRef_File_Episode ani
    ON vlp_selected.Hash = ani.Hash
       AND vlp_selected.FileSize = ani.FileSize
GROUP BY
    ani.AnimeID
";

    public IEnumerable<MediaSeries> GetWithDuplicateFiles()
    {
        var ids = Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();

            return session.CreateSQLQuery(DuplicateFilesQuery)
                .AddScalar("AnimeID", NHibernateUtil.Int32)
                .List<int>();
        });

        return ids
            .Distinct()
            .Select(GetByAnimeID)
            .WhereNotNull();
    }

    public const string MissingEpisodesCollectingQuery = @"SELECT ser.AniDB_ID FROM MediaSeries AS ser WHERE ser.MissingEpisodeCountGroups > 0";

    public const string MissingEpisodesQuery = @"SELECT ser.AniDB_ID FROM MediaSeries AS ser WHERE ser.MissingEpisodeCount > 0";

    public IEnumerable<MediaSeries> GetWithMissingEpisodes(bool collecting)
    {
        var ids = Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();

            var query = collecting ? MissingEpisodesCollectingQuery : MissingEpisodesQuery;
            return session.CreateSQLQuery(query)
                .AddScalar("AniDB_ID", NHibernateUtil.Int32)
                .List<int>();
        });

        return ids
            .Distinct()
            .Select(GetByAnimeID)
            .WhereNotNull();
    }

    public ImageEntityType[] GetAllImageTypes()
        => [ImageEntityType.Backdrop, ImageEntityType.Banner, ImageEntityType.Logo, ImageEntityType.Poster];

    public IEnumerable<int> GetAllYears()
    {
        var anime = RepoFactory.MediaSeries.GetAll().Select(a => RepoFactory.AniDB_Anime.GetByAnimeID(a.AniDB_ID)).Where(a => a?.AirDate != null).ToList();
        if (anime.Count == 0) yield break;
        var minDate = anime.Min(a => a!.AirDate!.Value);
        var maxDate = anime.Max(o => o!.EndDate ?? DateTime.Today);

        for (var year = minDate.Year; year <= maxDate.Year; year++)
        {
            var yearStart = new DateTime(year, 1, 1);
            var yearEnd = new DateTime(year, 12, 31);

            if (anime.Any(o => o!.AirDate <= yearEnd && (o.EndDate >= yearStart || o.EndDate == null)))
            {
                yield return year;
            }
        }
    }

    public SortedSet<(int Year, YearlySeason Season)> GetAllSeasons()
    {
        var anime = GetAll().Select(a => RepoFactory.AniDB_Anime.GetByAnimeID(a.AniDB_ID)).Where(a => a?.AirDate != null).ToList();
        return GetAllSeasons(anime!);
    }

    public static SortedSet<(int Year, YearlySeason Season)> GetAllSeasons(IEnumerable<AniDB_Anime> anime)
    {
        var seasons = new SortedSet<(int Year, YearlySeason Season)>();
        foreach (var current in anime)
            foreach (var tuple in current.YearlySeasons)
                seasons.Add(tuple);

        return seasons;
    }
}
