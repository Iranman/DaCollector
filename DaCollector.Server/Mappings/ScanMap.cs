using FluentNHibernate.Mapping;
using DaCollector.Server.Models.Legacy;
using DaCollector.Server.Server;

namespace DaCollector.Server.Mappings;

public class ScanMap : ClassMap<Scan>
{
    public ScanMap()
    {
        Table("Scan");

        Not.LazyLoad();
        Id(x => x.ScanID);
        Map(x => x.CreationTIme).Not.Nullable();
        Map(x => x.ImportFolders).Not.Nullable();
        Map(x => x.Status).Not.Nullable().CustomType<ScanStatus>();
    }
}
