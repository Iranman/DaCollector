using FluentNHibernate.Mapping;
using DaCollector.Server.Models.DaCollector;

namespace DaCollector.Server.Mappings;

public class MediaGroupMap : ClassMap<MediaGroup>
{
    public MediaGroupMap()
    {
        Table("MediaGroup");
        Not.LazyLoad();
        Id(x => x.MediaGroupID);
        Map(x => x.AnimeGroupParentID);
        Map(x => x.DefaultAnimeSeriesID);
        Map(x => x.MainAniDBAnimeID);
        Map(x => x.DateTimeCreated).Not.Nullable();
        Map(x => x.DateTimeUpdated).Not.Nullable();
        Map(x => x.Description).CustomType("StringClob").CustomSqlType("nvarchar(max)");
        Map(x => x.GroupName);
        Map(x => x.IsManuallyNamed).Not.Nullable();
        Map(x => x.OverrideDescription).Not.Nullable();
        Map(x => x.EpisodeAddedDate);
        Map(x => x.LatestEpisodeAirDate);
        Map(x => x.MissingEpisodeCount).Not.Nullable();
        Map(x => x.MissingEpisodeCountGroups).Not.Nullable();
    }
}
