using FluentNHibernate.Mapping;
using DaCollector.Abstractions.User.Enums;
using DaCollector.Server.Databases.NHibernate;
using DaCollector.Server.Models.DaCollector;

namespace DaCollector.Server.Mappings;

public class MediaSeries_UserMap : ClassMap<MediaSeries_User>
{
    public MediaSeries_UserMap()
    {
        Table("MediaSeries_User");

        Not.LazyLoad();
        Id(x => x.MediaSeries_UserID);
        Map(x => x.JMMUserID).Not.Nullable();
        Map(x => x.MediaSeriesID).Not.Nullable();
        Map(x => x.PlayedCount).Not.Nullable();
        Map(x => x.StoppedCount).Not.Nullable();
        Map(x => x.UnwatchedEpisodeCount).Not.Nullable();
        Map(x => x.WatchedCount).Not.Nullable();
        Map(x => x.WatchedDate);
        Map(x => x.WatchedEpisodeCount).Not.Nullable();
        Map(x => x.LastEpisodeUpdate);
        Map(x => x.LastVideoUpdate);
        Map(x => x.HiddenUnwatchedEpisodeCount).Not.Nullable();
        Map(x => x.IsFavorite).Not.Nullable();
        Map(x => x.AbsoluteUserRating);
        Map(x => x.UserRatingVoteType).CustomType<SeriesVoteType>();
        Map(x => x.UserTags).Not.Nullable().CustomType<StringListConverter>();
        Map(x => x.LastUpdated).Not.Nullable();
    }
}
