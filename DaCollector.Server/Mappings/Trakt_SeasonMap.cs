using FluentNHibernate.Mapping;
using DaCollector.Server.Models.Trakt;

namespace DaCollector.Server.Mappings;

public class Trakt_SeasonMap : ClassMap<Trakt_Season>
{
    public Trakt_SeasonMap()
    {
        Not.LazyLoad();
        Id(x => x.Trakt_SeasonID);

        Map(x => x.Season).Not.Nullable();
        Map(x => x.Trakt_ShowID).Not.Nullable();
        Map(x => x.URL);
    }
}
