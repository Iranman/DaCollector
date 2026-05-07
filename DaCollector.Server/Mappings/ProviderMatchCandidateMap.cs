using FluentNHibernate.Mapping;
using DaCollector.Server.Models.Internal;

namespace DaCollector.Server.Mappings;

public class ProviderMatchCandidateMap : ClassMap<ProviderMatchCandidate>
{
    public ProviderMatchCandidateMap()
    {
        Table("ProviderMatchCandidate");

        Not.LazyLoad();
        Id(x => x.ProviderMatchCandidateID);

        Map(x => x.MediaSeriesID).Not.Nullable().UniqueKey("UIX_ProviderMatchCandidate");
        Map(x => x.Provider).Not.Nullable().UniqueKey("UIX_ProviderMatchCandidate");
        Map(x => x.ProviderItemID).Not.Nullable().UniqueKey("UIX_ProviderMatchCandidate");
        Map(x => x.ProviderType).Not.Nullable().UniqueKey("UIX_ProviderMatchCandidate");
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
