using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using DaCollector.Abstractions.Metadata.Enums;
using DaCollector.Server.Databases;
using DaCollector.Server.Models.AniDB;

#nullable enable
namespace DaCollector.Server.Repositories.Cached.AniDB;

public class AniDB_Episode_PreferredImageRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<AniDB_Episode_PreferredImage, int>(databaseFactory)
{
    private PocoIndex<int, AniDB_Episode_PreferredImage, int>? _episodeIDs;

    private PocoIndex<int, AniDB_Episode_PreferredImage, (DataSource, ImageEntityType, int)>? _imageTypes;

    protected override int SelectKey(AniDB_Episode_PreferredImage entity)
        => entity.AniDB_Episode_PreferredImageID;

    public override void PopulateIndexes()
    {
        _episodeIDs = Cache.CreateIndex(a => a.AnidbEpisodeID);
        _imageTypes = Cache.CreateIndex(a => (a.ImageSource, a.ImageType, a.ImageID));
    }

    public AniDB_Episode_PreferredImage? GetByAnidbEpisodeIDAndType(int episodeID, ImageEntityType imageType)
        => GetByEpisodeID(episodeID).FirstOrDefault(a => a.ImageType == imageType);

    public AniDB_Episode_PreferredImage? GetByAnidbEpisodeIDAndTypeAndSource(int episodeID, ImageEntityType imageType, DataSource imageSource)
        => GetByEpisodeID(episodeID).FirstOrDefault(a => a.ImageType == imageType && a.ImageSource == imageSource);

    public IReadOnlyList<AniDB_Episode_PreferredImage> GetByEpisodeID(int episodeId)
        => ReadLock(() => _episodeIDs!.GetMultiple(episodeId));

    public IReadOnlyList<AniDB_Episode_PreferredImage> GetByImageSourceAndTypeAndID(DataSource imageSource, ImageEntityType imageType, int imageID)
        => ReadLock(() => _imageTypes!.GetMultiple((imageSource, imageType, imageID)));
}
