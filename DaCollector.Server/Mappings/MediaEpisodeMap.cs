using FluentNHibernate.Mapping;
using DaCollector.Server.Models.DaCollector;

namespace DaCollector.Server.Mappings;

public class MediaEpisodeMap : ClassMap<MediaEpisode>
{
    public MediaEpisodeMap()
    {
        Table("MediaEpisode");

        Not.LazyLoad();
        Id(x => x.MediaEpisodeID);

        Map(x => x.AniDB_EpisodeID).Not.Nullable();
        Map(x => x.MediaSeriesID).Not.Nullable();
        Map(x => x.DateTimeCreated).Not.Nullable();
        Map(x => x.DateTimeUpdated).Not.Nullable();
        Map(x => x.IsHidden).Not.Nullable();
        Map(x => x.EpisodeNameOverride);
    }
}
