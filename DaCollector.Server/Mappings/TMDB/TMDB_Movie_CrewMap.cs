using FluentNHibernate.Mapping;
using DaCollector.Server.Models.TMDB;

namespace DaCollector.Server.Mappings;

public class TMDB_Movie_CrewMap : ClassMap<TMDB_Movie_Crew>
{
    public TMDB_Movie_CrewMap()
    {
        Table("TMDB_Movie_Crew");

        Not.LazyLoad();
        Id(x => x.TMDB_Movie_CrewID);

        Map(x => x.TmdbMovieID).Not.Nullable();
        Map(x => x.TmdbPersonID).Not.Nullable();
        Map(x => x.TmdbCreditID).Not.Nullable();
        Map(x => x.Job).Not.Nullable();
        Map(x => x.Department).Not.Nullable();
    }
}
