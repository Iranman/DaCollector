using FluentNHibernate.Mapping;
using DaCollector.Server.Models.TMDB;
using DaCollector.Server.Providers.TMDB;

namespace DaCollector.Server.Mappings;

public class TMDB_AlternateOrderingMap : ClassMap<TMDB_AlternateOrdering>
{
    public TMDB_AlternateOrderingMap()
    {
        Table("TMDB_AlternateOrdering");

        Not.LazyLoad();
        Id(x => x.TMDB_AlternateOrderingID);

        Map(x => x.TmdbShowID).Not.Nullable();
        Map(x => x.TmdbNetworkID);
        Map(x => x.TmdbEpisodeGroupCollectionID).Not.Nullable();
        Map(x => x.EnglishTitle).Not.Nullable();
        Map(x => x.EnglishOverview).Not.Nullable();
        Map(x => x.EpisodeCount).Not.Nullable();
        Map(x => x.HiddenEpisodeCount).Not.Nullable();
        Map(x => x.SeasonCount).Not.Nullable();
        Map(x => x.Type).Not.Nullable().CustomType<AlternateOrderingType>();
        Map(x => x.CreatedAt).Not.Nullable();
        Map(x => x.LastUpdatedAt).Not.Nullable();
    }
}
