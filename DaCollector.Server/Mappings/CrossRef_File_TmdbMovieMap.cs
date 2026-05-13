using FluentNHibernate.Mapping;
using DaCollector.Server.Models.CrossReference;

namespace DaCollector.Server.Mappings;

public class CrossRef_File_TmdbMovieMap : ClassMap<CrossRef_File_TmdbMovie>
{
    public CrossRef_File_TmdbMovieMap()
    {
        Table("CrossRef_File_TmdbMovie");

        Not.LazyLoad();
        Id(x => x.CrossRef_File_TmdbMovieID);

        Map(x => x.VideoLocalID).Not.Nullable();
        Map(x => x.TmdbMovieID).Not.Nullable();
        Map(x => x.IsManuallyLinked).Not.Nullable();
        Map(x => x.CreatedAt).Not.Nullable();
        Map(x => x.UpdatedAt).Not.Nullable();
    }
}
