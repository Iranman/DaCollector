using FluentNHibernate.Mapping;
using DaCollector.Server.Models.Internal;

namespace DaCollector.Server.Mappings;

public class MediaFileReviewStateMap : ClassMap<MediaFileReviewState>
{
    public MediaFileReviewStateMap()
    {
        Table("MediaFileReviewState");
        Not.LazyLoad();
        Id(x => x.MediaFileReviewStateID);
        Map(x => x.VideoLocalID).Not.Nullable().UniqueKey("UIX_MediaFileReviewState_VideoLocalID");
        Map(x => x.Status).Not.Nullable();
        Map(x => x.ParsedKind).Not.Nullable();
        Map(x => x.ParsedTitle);
        Map(x => x.ParsedYear);
        Map(x => x.ParsedShowTitle);
        Map(x => x.ParsedSeasonNumber);
        Map(x => x.ParsedEpisodeNumbersJson).Not.Nullable().Length(4000);
        Map(x => x.ParsedAirDate);
        Map(x => x.ParsedExternalIdsJson).Not.Nullable().Length(4000);
        Map(x => x.ParsedQuality);
        Map(x => x.ParsedSource);
        Map(x => x.ParsedEdition);
        Map(x => x.ParsedVideoCodec);
        Map(x => x.ParsedAudioCodec);
        Map(x => x.ParsedAudioChannels);
        Map(x => x.ParsedHdrFormatsJson).Not.Nullable().Length(4000);
        Map(x => x.ParsedWarningsJson).Not.Nullable().Length(4000);
        Map(x => x.ManualEntityType);
        Map(x => x.ManualEntityID);
        Map(x => x.ManualProvider);
        Map(x => x.ManualProviderID);
        Map(x => x.ManualTitle);
        Map(x => x.Locked).Not.Nullable();
        Map(x => x.IgnoredReason);
        Map(x => x.CreatedAt).Not.Nullable();
        Map(x => x.UpdatedAt).Not.Nullable();
        Map(x => x.LastParsedAt).Not.Nullable();
    }
}
