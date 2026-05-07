using FluentNHibernate.Mapping;
using DaCollector.Server.Databases.NHibernate;
using DaCollector.Server.Models.TVDB;

namespace DaCollector.Server.Mappings.TVDB;

public class TVDB_EpisodeMap : ClassMap<TVDB_Episode>
{
    public TVDB_EpisodeMap()
    {
        Table("TVDB_Episode");

        Not.LazyLoad();
        Id(x => x.TVDB_EpisodeID);

        Map(x => x.TvdbEpisodeID).Not.Nullable();
        Map(x => x.TvdbShowID).Not.Nullable();
        Map(x => x.TvdbSeasonID).Nullable();
        Map(x => x.SeasonNumber).Not.Nullable();
        Map(x => x.EpisodeNumber).Not.Nullable();
        Map(x => x.Name).Not.Nullable();
        Map(x => x.Overview).Not.Nullable();
        Map(x => x.RuntimeMinutes).Nullable();
        Map(x => x.AiredAt).CustomType<DateOnlyConverter>();
        Map(x => x.CreatedAt).Not.Nullable();
        Map(x => x.LastUpdatedAt).Not.Nullable();
    }
}
