using FluentNHibernate.Mapping;
using DaCollector.Server.Models.CrossReference;

namespace DaCollector.Server.Mappings;

public class CrossRef_CustomTagMap : ClassMap<CrossRef_CustomTag>
{
    public CrossRef_CustomTagMap()
    {
        Not.LazyLoad();
        Id(x => x.CrossRef_CustomTagID);

        Map(x => x.CustomTagID).Not.Nullable();
        Map(x => x.CrossRefID).Not.Nullable();
        Map(x => x.CrossRefType).Not.Nullable();
    }
}
