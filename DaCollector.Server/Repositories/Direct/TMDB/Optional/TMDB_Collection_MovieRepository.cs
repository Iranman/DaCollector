#nullable enable
using System.Collections.Generic;
using System.Linq;
using DaCollector.Server.Databases;
using DaCollector.Server.Models.TMDB;

namespace DaCollector.Server.Repositories.Direct.TMDB.Optional;

public class TMDB_Collection_MovieRepository : BaseDirectRepository<TMDB_Collection_Movie, int>
{
    public IReadOnlyList<TMDB_Collection_Movie> GetByTmdbCollectionID(int collectionId)
    {
        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            return session
                .Query<TMDB_Collection_Movie>()
                .Where(a => a.TmdbCollectionID == collectionId)
                .ToList();
        });
    }

    public TMDB_Collection_Movie? GetByTmdbMovieID(int movieId)
    {
        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            return session
                .Query<TMDB_Collection_Movie>()
                .Where(a => a.TmdbMovieID == movieId)
                .Take(1)
                .SingleOrDefault();
        });
    }

    public TMDB_Collection_MovieRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
    }
}
