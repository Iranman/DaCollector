using FluentNHibernate.Mapping;
using DaCollector.Server.Databases.NHibernate;
using DaCollector.Server.Models.TMDB;
using DaCollector.Server.Providers.TMDB;

namespace DaCollector.Server.Mappings;

public class TMDB_PersonMap : ClassMap<TMDB_Person>
{
    public TMDB_PersonMap()
    {
        Table("TMDB_Person");

        Not.LazyLoad();
        Id(x => x.TMDB_PersonID);

        Map(x => x.TmdbPersonID).Not.Nullable();
        Map(x => x.EnglishName).Not.Nullable();
        Map(x => x.EnglishBiography).Not.Nullable();
        Map(x => x.Aliases).Not.Nullable().CustomType<StringListConverter>();
        Map(x => x.Gender).Not.Nullable().CustomType<PersonGender>();
        Map(x => x.IsRestricted).Not.Nullable();
        Map(x => x.BirthDay).CustomType<DateOnlyConverter>();
        Map(x => x.DeathDay).CustomType<DateOnlyConverter>();
        Map(x => x.PlaceOfBirth);
        Map(x => x.CreatedAt).Not.Nullable();
        Map(x => x.LastUpdatedAt).Not.Nullable();
        Map(x => x.LastOrphanedAt);
    }
}
