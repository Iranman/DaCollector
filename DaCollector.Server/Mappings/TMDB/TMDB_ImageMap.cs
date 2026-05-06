using FluentNHibernate.Mapping;
using DaCollector.Server.Databases.NHibernate;
using DaCollector.Server.Models.TMDB;

namespace DaCollector.Server.Mappings;

public class TMDB_ImageMap : ClassMap<TMDB_Image>
{
    public TMDB_ImageMap()
    {
        Table("TMDB_Image");

        Not.LazyLoad();
        Id(x => x.TMDB_ImageID);

        Map(x => x.IsEnabled);
        Map(x => x.Width).Not.Nullable();
        Map(x => x.Height).Not.Nullable();
        Map(x => x.Language).Not.Nullable().CustomType<TitleLanguageConverter>();
        Map(x => x.RemoteFileName).Not.Nullable();
        Map(x => x.UserRating).Not.Nullable();
        Map(x => x.UserVotes).Not.Nullable();
    }
}
