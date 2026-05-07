using FluentNHibernate.Mapping;
using DaCollector.Server.Models.DaCollector;

namespace DaCollector.Server.Mappings;

public class MediaGroup_UserMap : ClassMap<MediaGroup_User>
{
    public MediaGroup_UserMap()
    {
        Table("MediaGroup_User");

        Not.LazyLoad();
        Id(x => x.MediaGroup_UserID);
        Map(x => x.JMMUserID);
        Map(x => x.MediaGroupID);
        Map(x => x.PlayedCount).Not.Nullable();
        Map(x => x.StoppedCount).Not.Nullable();
        Map(x => x.UnwatchedEpisodeCount).Not.Nullable();
        Map(x => x.WatchedCount).Not.Nullable();
        Map(x => x.WatchedDate);
        Map(x => x.WatchedEpisodeCount);
    }
}
