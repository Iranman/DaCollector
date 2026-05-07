using FluentNHibernate.Mapping;
using DaCollector.Server.Databases.NHibernate;
using DaCollector.Server.Models.DaCollector;

namespace DaCollector.Server.Mappings;

public class MediaEpisode_UserMap : ClassMap<MediaEpisode_User>
{
    public MediaEpisode_UserMap()
    {
        Table("MediaEpisode_User");
        Not.LazyLoad();
        Id(x => x.MediaEpisode_UserID);

        Map(x => x.MediaEpisodeID).Not.Nullable();
        Map(x => x.MediaSeriesID).Not.Nullable();
        Map(x => x.JMMUserID).Not.Nullable();
        Map(x => x.PlayedCount).Not.Nullable();
        Map(x => x.StoppedCount).Not.Nullable();
        Map(x => x.WatchedCount).Not.Nullable();
        Map(x => x.WatchedDate);
        Map(x => x.IsFavorite).Not.Nullable();
        Map(x => x.AbsoluteUserRating);
        Map(x => x.UserTags).Not.Nullable().CustomType<StringListConverter>();
        Map(x => x.LastUpdated).Not.Nullable();
    }
}
