using FluentNHibernate.Mapping;
using DaCollector.Server.Models.TVDB;

namespace DaCollector.Server.Mappings.TVDB;

public class TVDB_SeasonMap : ClassMap<TVDB_Season>
{
    public TVDB_SeasonMap()
    {
        Table("TVDB_Season");

        Not.LazyLoad();
        Id(x => x.TVDB_SeasonID);

        Map(x => x.TvdbSeasonID).Not.Nullable();
        Map(x => x.TvdbShowID).Not.Nullable();
        Map(x => x.SeasonNumber).Not.Nullable();
        Map(x => x.SeasonType).Not.Nullable();
        Map(x => x.Name).Not.Nullable();
        Map(x => x.Overview).Not.Nullable();
        Map(x => x.EpisodeCount).Not.Nullable();
        Map(x => x.Year).Nullable();
        Map(x => x.PosterPath).Nullable();
        Map(x => x.CreatedAt).Not.Nullable();
        Map(x => x.LastUpdatedAt).Not.Nullable();
    }
}
