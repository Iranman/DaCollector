using FluentNHibernate.Mapping;
using DaCollector.Server.Models.DaCollector;
using DaCollector.Server.Server;

namespace DaCollector.Server.Mappings;

public class MediaSeriesMap : ClassMap<MediaSeries>
{
    public MediaSeriesMap()
    {
        Table("MediaSeries");
        Not.LazyLoad();
        Id(x => x.MediaSeriesID);

        Map(x => x.AniDB_ID).Not.Nullable();
        Map(x => x.TVDB_ShowID).Nullable();
        Map(x => x.TVDB_MovieID).Nullable();
        Map(x => x.TMDB_ShowID).Nullable();
        Map(x => x.TMDB_MovieID).Nullable();
        Map(x => x.MediaGroupID).Not.Nullable();
        Map(x => x.DateTimeCreated).Not.Nullable();
        Map(x => x.DateTimeUpdated).Not.Nullable();
        Map(x => x.DefaultAudioLanguage);
        Map(x => x.DefaultSubtitleLanguage);
        Map(x => x.LatestLocalEpisodeNumber).Not.Nullable();
        Map(x => x.EpisodeAddedDate);
        Map(x => x.LatestEpisodeAirDate);
        Map(x => x.MissingEpisodeCount).Not.Nullable();
        Map(x => x.MissingEpisodeCountGroups).Not.Nullable();
        Map(x => x.HiddenMissingEpisodeCount).Not.Nullable();
        Map(x => x.HiddenMissingEpisodeCountGroups).Not.Nullable();
        Map(x => x.SeriesNameOverride);
        Map(x => x.AirsOn);
        Map(x => x.UpdatedAt).Not.Nullable();
        Map(x => x.DisableAutoMatchFlags).Not.Nullable().CustomType<DisabledAutoMatchFlag>();
    }
}
