using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using DaCollector.Abstractions.Extensions;
using DaCollector.Server.Databases;
using DaCollector.Server.Models.AniDB;

#nullable enable
namespace DaCollector.Server.Repositories.Cached.AniDB;

public class AniDB_CharacterRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<AniDB_Character, int>(databaseFactory)
{
    private PocoIndex<int, AniDB_Character, int>? _characterIDs;

    protected override int SelectKey(AniDB_Character entity)
        => entity.AniDB_CharacterID;

    public override void PopulateIndexes()
    {
        _characterIDs = Cache.CreateIndex(a => a.CharacterID);
    }

    public AniDB_Character? GetByCharacterID(int characterID)
        => ReadLock(() => _characterIDs!.GetOne(characterID));

    public IReadOnlyList<AniDB_Character> GetCharactersForAnime(int animeID)
        => ReadLock(() => RepoFactory.AniDB_Anime_Character.GetByAnimeID(animeID).Select(xref => GetByCharacterID(xref.CharacterID)).WhereNotNull().ToList());

    public AniDB_Character? GetByName(string creatorName)
    {
        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            var id = session.Query<AniDB_Character>()
                .Where(a => a.Name == creatorName)
                .Take(1)
                .SingleOrDefault()?.AniDB_CharacterID;
            if (id.HasValue)
                return Cache.Get(id.Value);
            return null;
        });
    }
}
