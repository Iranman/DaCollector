using FluentNHibernate.Mapping;
using DaCollector.Server.Models.TMDB;
using DaCollector.Server.Server;

namespace DaCollector.Server.Mappings;

public class TMDB_TitleMap : ClassMap<TMDB_Title>
{
    public TMDB_TitleMap()
    {
        Table("TMDB_Title");

        Not.LazyLoad();
        Id(x => x.TMDB_TitleID);

        Map(x => x.ParentID).Not.Nullable();
        Map(x => x.ParentType).Not.Nullable().CustomType<ForeignEntityType>();
        Map(x => x.LanguageCode).Not.Nullable();
        Map(x => x.CountryCode).Not.Nullable();
        Map(x => x.Value).Not.Nullable();
    }
}
