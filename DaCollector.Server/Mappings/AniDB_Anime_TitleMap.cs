using FluentNHibernate.Mapping;
using DaCollector.Server.Databases.NHibernate;
using DaCollector.Server.Models.AniDB;

namespace DaCollector.Server.Mappings;

public class AniDB_Anime_TitleMap : ClassMap<AniDB_Anime_Title>
{
    public AniDB_Anime_TitleMap()
    {
        Table("AniDB_Anime_Title");
        Not.LazyLoad();
        Id(x => x.AniDB_Anime_TitleID);

        Map(x => x.AnimeID).Not.Nullable();
        Map(x => x.Language).CustomType<TitleLanguageConverter>().Not.Nullable();
        Map(x => x.Title).Not.Nullable();
        Map(x => x.TitleType).CustomType<TitleTypeConverter>().Not.Nullable();
    }
}
