using FluentNHibernate.Mapping;
using DaCollector.Server.Models.TMDB;
using DaCollector.Server.Server;

namespace DaCollector.Server.Mappings;

public class TMDB_OverviewMap : ClassMap<TMDB_Overview>
{
    public TMDB_OverviewMap()
    {
        Table("TMDB_Overview");

        Not.LazyLoad();
        Id(x => x.TMDB_OverviewID);

        Map(x => x.ParentID).Not.Nullable();
        Map(x => x.ParentType).Not.Nullable().CustomType<ForeignEntityType>();
        Map(x => x.LanguageCode).Not.Nullable();
        Map(x => x.CountryCode).Not.Nullable();
        Map(x => x.Value).Not.Nullable();
    }
}
