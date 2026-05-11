using FluentNHibernate.Mapping;
using DaCollector.Server.Models.Internal;

namespace DaCollector.Server.Mappings;

public class MediaFileMatchCandidateMap : ClassMap<MediaFileMatchCandidate>
{
    public MediaFileMatchCandidateMap()
    {
        Table("MediaFileMatchCandidate");
        Not.LazyLoad();
        Id(x => x.MediaFileMatchCandidateID);
        Map(x => x.VideoLocalID).Not.Nullable().UniqueKey("UIX_MediaFileMatchCandidate");
        Map(x => x.Provider).Not.Nullable().UniqueKey("UIX_MediaFileMatchCandidate");
        Map(x => x.ProviderItemID).Not.Nullable().UniqueKey("UIX_MediaFileMatchCandidate");
        Map(x => x.ProviderType).Not.Nullable().UniqueKey("UIX_MediaFileMatchCandidate");
        Map(x => x.Title).Not.Nullable();
        Map(x => x.Year).Nullable();
        Map(x => x.ConfidenceScore).Not.Nullable();
        Map(x => x.ReasonsJson).Not.Nullable();
        Map(x => x.Status).Not.Nullable();
        Map(x => x.ReviewedAt).Nullable();
        Map(x => x.CreatedAt).Not.Nullable();
        Map(x => x.UpdatedAt).Not.Nullable();
    }
}
