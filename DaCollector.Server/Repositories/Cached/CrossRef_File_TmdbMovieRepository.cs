using System.Collections.Generic;
using NutzCode.InMemoryIndex;
using DaCollector.Server.Databases;
using DaCollector.Server.Models.CrossReference;

#nullable enable
namespace DaCollector.Server.Repositories.Cached;

public class CrossRef_File_TmdbMovieRepository : BaseCachedRepository<CrossRef_File_TmdbMovie, int>
{
    private PocoIndex<int, CrossRef_File_TmdbMovie, int>? _videoLocalIDs;

    private PocoIndex<int, CrossRef_File_TmdbMovie, int>? _tmdbMovieIDs;

    public CrossRef_File_TmdbMovieRepository(DatabaseFactory databaseFactory) : base(databaseFactory) { }

    protected override int SelectKey(CrossRef_File_TmdbMovie entity)
        => entity.CrossRef_File_TmdbMovieID;

    public override void PopulateIndexes()
    {
        _videoLocalIDs = Cache.CreateIndex(a => a.VideoLocalID);
        _tmdbMovieIDs = Cache.CreateIndex(a => a.TmdbMovieID);
    }

    public IReadOnlyList<CrossRef_File_TmdbMovie> GetByVideoLocalID(int videoLocalID)
        => ReadLock(() => _videoLocalIDs!.GetMultiple(videoLocalID));

    public IReadOnlyList<CrossRef_File_TmdbMovie> GetByTmdbMovieID(int tmdbMovieID)
        => ReadLock(() => _tmdbMovieIDs!.GetMultiple(tmdbMovieID));

    public CrossRef_File_TmdbMovie? GetByVideoLocalAndTmdbMovie(int videoLocalID, int tmdbMovieID)
        => ReadLock(() => _videoLocalIDs!.GetMultiple(videoLocalID)
            .Find(x => x.TmdbMovieID == tmdbMovieID));
}
