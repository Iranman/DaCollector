using FluentNHibernate.Mapping;
using DaCollector.Server.Databases.NHibernate;
using DaCollector.Server.Models.TVDB;

namespace DaCollector.Server.Mappings.TVDB;

public class TVDB_ShowMap : ClassMap<TVDB_Show>
{
    public TVDB_ShowMap()
    {
        Table("TVDB_Show");

        Not.LazyLoad();
        Id(x => x.TVDB_ShowID);

        Map(x => x.TvdbShowID).Not.Nullable();
        Map(x => x.Name).Not.Nullable();
        Map(x => x.Overview).Not.Nullable();
        Map(x => x.FirstAiredAt).CustomType<DateOnlyConverter>();
        Map(x => x.LastAiredAt).CustomType<DateOnlyConverter>();
        Map(x => x.Status).Not.Nullable();
        Map(x => x.OriginalLanguage).Not.Nullable();
        Map(x => x.OriginalCountry).Not.Nullable();
        Map(x => x.SeasonCount).Not.Nullable();
        Map(x => x.EpisodeCount).Not.Nullable();
        Map(x => x.Network).Not.Nullable();
        Map(x => x.Genres).Not.Nullable().CustomType<StringListConverter>();
        Map(x => x.UserRating).Not.Nullable();
        Map(x => x.Year).Nullable();
        Map(x => x.PosterPath).Nullable();
        Map(x => x.CreatedAt).Not.Nullable();
        Map(x => x.LastUpdatedAt).Not.Nullable();
    }
}
