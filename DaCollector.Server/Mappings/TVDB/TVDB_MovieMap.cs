using FluentNHibernate.Mapping;
using DaCollector.Server.Databases.NHibernate;
using DaCollector.Server.Models.TVDB;

namespace DaCollector.Server.Mappings.TVDB;

public class TVDB_MovieMap : ClassMap<TVDB_Movie>
{
    public TVDB_MovieMap()
    {
        Table("TVDB_Movie");

        Not.LazyLoad();
        Id(x => x.TVDB_MovieID);

        Map(x => x.TvdbMovieID).Not.Nullable();
        Map(x => x.Name).Not.Nullable();
        Map(x => x.Overview).Not.Nullable();
        Map(x => x.ReleasedAt).CustomType<DateOnlyConverter>();
        Map(x => x.RuntimeMinutes).Nullable();
        Map(x => x.Status).Not.Nullable();
        Map(x => x.OriginalLanguage).Not.Nullable();
        Map(x => x.OriginalCountry).Not.Nullable();
        Map(x => x.Genres).Not.Nullable().CustomType<StringListConverter>();
        Map(x => x.UserRating).Not.Nullable();
        Map(x => x.Year).Nullable();
        Map(x => x.PosterPath).Nullable();
        Map(x => x.CreatedAt).Not.Nullable();
        Map(x => x.LastUpdatedAt).Not.Nullable();
    }
}
