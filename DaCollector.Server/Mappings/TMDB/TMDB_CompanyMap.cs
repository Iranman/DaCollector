using FluentNHibernate.Mapping;
using DaCollector.Server.Models.TMDB;

namespace DaCollector.Server.Mappings;

public class TMDB_CompanyMap : ClassMap<TMDB_Company>
{
    public TMDB_CompanyMap()
    {
        Table("TMDB_Company");

        Not.LazyLoad();
        Id(x => x.TMDB_CompanyID);

        Map(x => x.TmdbCompanyID).Not.Nullable();
        Map(x => x.Name).Not.Nullable();
        Map(x => x.CountryOfOrigin).Not.Nullable();
    }
}
