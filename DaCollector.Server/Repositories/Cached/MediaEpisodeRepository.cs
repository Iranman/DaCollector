using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NHibernate;
using NutzCode.InMemoryIndex;
using DaCollector.Abstractions.Metadata.Enums;
using DaCollector.Abstractions.Extensions;
using DaCollector.Server.Databases;
using DaCollector.Server.Extensions;
using DaCollector.Server.Models.DaCollector;
using DaCollector.Server.Services;

#nullable enable
namespace DaCollector.Server.Repositories.Cached;

public class MediaEpisodeRepository : BaseCachedRepository<MediaEpisode, int>
{
    private PocoIndex<int, MediaEpisode, int>? _seriesIDs;

    private PocoIndex<int, MediaEpisode, int?>? _anidbEpisodeIDs;

    public MediaEpisodeRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
        BeginDeleteCallback = cr =>
        {
            RepoFactory.MediaEpisode_User.Delete(
                RepoFactory.MediaEpisode_User.GetByEpisodeID(cr.MediaEpisodeID));
        };
    }

    protected override int SelectKey(MediaEpisode entity)
        => entity.MediaEpisodeID;

    public override void PopulateIndexes()
    {
        _seriesIDs = Cache.CreateIndex(a => a.MediaSeriesID);
        _anidbEpisodeIDs = Cache.CreateIndex(a => a.AniDB_EpisodeID);
    }

    public List<MediaEpisode> GetBySeriesID(int seriesID)
        => ReadLock(() => _seriesIDs!.GetMultiple(seriesID));

    public MediaEpisode? GetByAniDBEpisodeID(int episodeID)
        => ReadLock(() => _anidbEpisodeIDs!.GetOne(episodeID));

    /// <summary>
    /// Get the MediaEpisode
    /// </summary>
    /// <param name="name">The filename of the anime to search for.</param>
    /// <returns>the MediaEpisode given the file information</returns>
    public MediaEpisode? GetByFilename(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;

        var eps = RepoFactory.VideoLocalPlace.GetAll()
            .Where(v => name.Equals(v?.RelativePath?.Split(Path.DirectorySeparatorChar).LastOrDefault(), StringComparison.InvariantCultureIgnoreCase))
            .Select(a => RepoFactory.VideoLocal.GetByID(a.VideoID))
            .WhereNotNull()
            .SelectMany(a => GetByHash(a.Hash))
            .OrderBy(a => a.AniDB_Episode?.EpisodeType is EpisodeType.Episode)
            .ToArray();
        var ep = eps.FirstOrDefault(a => a.AniDB_Episode?.EpisodeType is EpisodeType.Episode);
        return ep ?? eps.FirstOrDefault();
    }


    /// <summary>
    /// Get all the MediaEpisode records associate with an AniDB_File record
    /// MediaEpisode.AniDB_EpisodeID -> AniDB_Episode.EpisodeID
    /// AniDB_Episode.EpisodeID -> CrossRef_File_Episode.EpisodeID
    /// CrossRef_File_Episode.Hash -> VideoLocal.Hash
    /// </summary>
    /// <param name="hash"></param>
    /// <returns></returns>
    public List<MediaEpisode> GetByHash(string hash)
    {
        if (string.IsNullOrEmpty(hash))
            return [];

        return RepoFactory.CrossRef_File_Episode.GetByEd2k(hash)
            .Select(a => GetByAniDBEpisodeID(a.EpisodeID))
            .WhereNotNull()
            .ToList();
    }

    private const string MultipleReleasesIgnoreVariationsWithAnimeQuery =
        @"SELECT ani.EpisodeID FROM VideoLocal AS vl JOIN CrossRef_File_Episode ani ON vl.Hash = ani.Hash WHERE ani.AnimeID = :animeID AND vl.IsVariation = 0 AND vl.Hash != '' GROUP BY ani.EpisodeID HAVING COUNT(ani.EpisodeID) > 1";
    private const string MultipleReleasesCountVariationsWithAnimeQuery =
        @"SELECT ani.EpisodeID FROM VideoLocal AS vl JOIN CrossRef_File_Episode ani ON vl.Hash = ani.Hash WHERE ani.AnimeID = :animeID AND vl.Hash != '' GROUP BY ani.EpisodeID HAVING COUNT(ani.EpisodeID) > 1";
    private const string MultipleReleasesIgnoreVariationsQuery =
        @"SELECT ani.EpisodeID FROM VideoLocal AS vl JOIN CrossRef_File_Episode ani ON vl.Hash = ani.Hash WHERE vl.IsVariation = 0 AND vl.Hash != '' GROUP BY ani.EpisodeID HAVING COUNT(ani.EpisodeID) > 1";
    private const string MultipleReleasesCountVariationsQuery =
        @"SELECT ani.EpisodeID FROM VideoLocal AS vl JOIN CrossRef_File_Episode ani ON vl.Hash = ani.Hash WHERE vl.Hash != '' GROUP BY ani.EpisodeID HAVING COUNT(ani.EpisodeID) > 1";

    public IEnumerable<MediaEpisode> GetWithMultipleReleases(bool ignoreVariations, int? animeID = null)
    {
        var ids = Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            if (animeID.HasValue && animeID.Value > 0)
            {
                var animeQuery = ignoreVariations ? MultipleReleasesIgnoreVariationsWithAnimeQuery : MultipleReleasesCountVariationsWithAnimeQuery;
                return session.CreateSQLQuery(animeQuery)
                    .AddScalar("EpisodeID", NHibernateUtil.Int32)
                    .SetParameter("animeID", animeID.Value)
                    .List<int>();
            }

            var query = ignoreVariations ? MultipleReleasesIgnoreVariationsQuery : MultipleReleasesCountVariationsQuery;
            return session.CreateSQLQuery(query)
                .AddScalar("EpisodeID", NHibernateUtil.Int32)
                .List<int>();
        });

        return ids
            .Select(GetByAniDBEpisodeID)
            .Select(episode => (episode, MetadataEpisode: episode?.AniDB_Episode))
            .Where(tuple => tuple.MetadataEpisode is not null)
            .OrderBy(tuple => tuple.MetadataEpisode!.AnimeID)
            .ThenBy(tuple => tuple.MetadataEpisode!.EpisodeType)
            .ThenBy(tuple => tuple.MetadataEpisode!.EpisodeNumber)
            .Select(tuple => tuple.episode!);
    }

    private const string DuplicateFilesWithAnimeQuery = @"
SELECT
    ani.EpisodeID
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
WHERE ani.AnimeID = :animeID
GROUP BY
    ani.EpisodeID
";

    private const string DuplicateFilesQuery = @"
SELECT
    ani.EpisodeID
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
    ani.EpisodeID
";

    public IEnumerable<MediaEpisode> GetWithDuplicateFiles(int? animeID = null)
    {
        var ids = Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            if (animeID.HasValue && animeID.Value > 0)
            {
                return session.CreateSQLQuery(DuplicateFilesWithAnimeQuery)
                    .AddScalar("EpisodeID", NHibernateUtil.Int32)
                    .SetParameter("animeID", animeID.Value)
                    .List<int>();
            }

            return session.CreateSQLQuery(DuplicateFilesQuery)
                .AddScalar("EpisodeID", NHibernateUtil.Int32)
                .List<int>();
        });

        return ids
            .Select(GetByAniDBEpisodeID)
            .Select(episode => (episode, MetadataEpisode: episode?.AniDB_Episode))
            .Where(tuple => tuple.MetadataEpisode is not null)
            .OrderBy(tuple => tuple.MetadataEpisode!.AnimeID)
            .ThenBy(tuple => tuple.MetadataEpisode!.EpisodeType)
            .ThenBy(tuple => tuple.MetadataEpisode!.EpisodeNumber)
            .Select(tuple => tuple.episode!);
    }

    public IEnumerable<MediaEpisode> GetMissing(bool collecting, int? animeID = null)
    {
        // NOTE: For comments about this code, see the MediaSeriesService.
        var allSeries = animeID.HasValue
            ? new List<MediaSeries?>([RepoFactory.MediaSeries.GetByAnimeID(animeID.Value)]).WhereNotNull()
            : RepoFactory.MediaSeries.GetWithMissingEpisodes(collecting);
        foreach (var series in allSeries)
        {
            var MediaType = series.AniDB_Anime!.MediaType;
            var episodeReleasedList = new MediaSeriesService.EpisodeList(MediaType);
            var episodeReleasedGroupList = new MediaSeriesService.EpisodeList(MediaType);
            var animeGroupStatuses = RepoFactory.AniDB_GroupStatus.GetByAnimeID(series.AniDB_ID ?? 0);
            var allEpisodes = series.AllAnimeEpisodes
                .Select(episode => (episode, MetadataEpisode: episode.AniDB_Episode!, videos: episode.VideoLocals))
                .Where(tuple => tuple.MetadataEpisode is not null)
                .ToList();
            var localReleaseGroups = allEpisodes
                .Where(tuple => tuple.MetadataEpisode.EpisodeType == EpisodeType.Episode)
                .SelectMany(a => a.videos
                    .Select(b => b.ReleaseGroup)
                    .WhereNotNull()
                    .Where(b => b.Source is "AniDB" && int.TryParse(b.ID, out var groupID) && groupID > 0)
                    .Select(b => int.Parse(b.ID))
                )
                .ToHashSet();
            foreach (var (episode, MetadataEpisode, videos) in allEpisodes)
            {
                if (MetadataEpisode.EpisodeType is not EpisodeType.Episode || videos.Count is not 0 || !MetadataEpisode.HasAired)
                    continue;

                if (animeGroupStatuses.Count is 0)
                {
                    episodeReleasedList.Add(episode, videos.Count is not 0);
                    continue;
                }

                var filteredGroups = animeGroupStatuses
                    .Where(status =>
                        status.CompletionState is 3 or 5 || // Complete or Finished
                        status.HasGroupReleasedEpisode(MetadataEpisode.EpisodeNumber)
                    )
                    .ToList();
                if (filteredGroups.Count is 0)
                    continue;

                episodeReleasedList.Add(episode, videos.Count is not 0);
                if (filteredGroups.Any(a => localReleaseGroups.Contains(a.GroupID)))
                    episodeReleasedGroupList.Add(episode, videos.Count is not 0);
            }

            foreach (var episodeStats in collecting ? episodeReleasedGroupList : episodeReleasedList)
            {
                if (episodeStats.Available)
                    continue;

                foreach (var episodeStat in episodeStats)
                    if (!episodeStat.Episode.IsHidden)
                        yield return episodeStat.Episode;
            }
        }
    }

    public IReadOnlyList<MediaEpisode> GetAllWatchedEpisodes(int userid, DateTime? after_date)
        => RepoFactory.MediaEpisode_User.GetByUserID(userid)
            .Where(a => a.IsWatched && a.WatchedDate > after_date).OrderBy(a => a.WatchedDate)
            .Select(a => a.MediaEpisode)
            .WhereNotNull()
            .ToList();

    public IReadOnlyList<MediaEpisode> GetEpisodesWithNoFiles(bool includeSpecials, bool includeOnlyAired = false)
        => GetAll()
            .Where(a =>
            {
                var MetadataEpisode = a.AniDB_Episode;
                if (MetadataEpisode is null || MetadataEpisode.HasAired)
                    return false;

                if (MetadataEpisode.EpisodeType is not EpisodeType.Episode and not EpisodeType.Special)
                    return false;

                if (!includeSpecials && MetadataEpisode.EpisodeType is EpisodeType.Special)
                    return false;

                if (includeOnlyAired && !MetadataEpisode.HasAired)
                    return false;

                return a.VideoLocals.Count == 0;
            })
            .OrderBy(a => a.MediaSeries?.PreferredTitle)
            .ThenBy(a => a.MediaSeriesID)
            .ToList();
}
