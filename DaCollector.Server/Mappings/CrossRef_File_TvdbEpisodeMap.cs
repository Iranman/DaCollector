using FluentNHibernate.Mapping;
using DaCollector.Server.Models.CrossReference;

namespace DaCollector.Server.Mappings;

public class CrossRef_File_TvdbEpisodeMap : ClassMap<CrossRef_File_TvdbEpisode>
{
    public CrossRef_File_TvdbEpisodeMap()
    {
        Table("CrossRef_File_TvdbEpisode");

        Not.LazyLoad();
        Id(x => x.CrossRef_File_TvdbEpisodeID);

        Map(x => x.VideoLocalID).Not.Nullable();
        Map(x => x.TvdbEpisodeID).Not.Nullable();
        Map(x => x.Percentage).Not.Nullable();
        Map(x => x.EpisodeOrder).Not.Nullable();
        Map(x => x.IsManuallyLinked).Not.Nullable();
        Map(x => x.CreatedAt).Not.Nullable();
        Map(x => x.UpdatedAt).Not.Nullable();
    }
}
