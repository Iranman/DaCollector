using FluentNHibernate.Mapping;
using DaCollector.Server.Models.DaCollector;

namespace DaCollector.Server.Mappings;

public class StoredRelocationPipeMap : ClassMap<StoredRelocationPipe>
{
    public StoredRelocationPipeMap()
    {
        Table("StoredRelocationPipe");
        Not.LazyLoad();
        Id(x => x.StoredRelocationPipeID);

        Map(x => x.ProviderID).Not.Nullable();
        Map(x => x.Name).Not.Nullable();
        Map(x => x.Configuration).Nullable();
    }
}
