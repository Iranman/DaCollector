using FluentNHibernate.Mapping;
using DaCollector.Server.Models.CrossReference;

namespace DaCollector.Server.Mappings;

public class CrossRef_File_TmdbEpisodeMap : ClassMap<CrossRef_File_TmdbEpisode>
{
    public CrossRef_File_TmdbEpisodeMap()
    {
        Table("CrossRef_File_TmdbEpisode");

        Not.LazyLoad();
        Id(x => x.CrossRef_File_TmdbEpisodeID);

        Map(x => x.VideoLocalID).Not.Nullable();
        Map(x => x.TmdbEpisodeID).Not.Nullable();
        Map(x => x.Percentage).Not.Nullable();
        Map(x => x.EpisodeOrder).Not.Nullable();
        Map(x => x.IsManuallyLinked).Not.Nullable();
        Map(x => x.CreatedAt).Not.Nullable();
        Map(x => x.UpdatedAt).Not.Nullable();
    }
}
