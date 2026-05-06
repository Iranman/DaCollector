using FluentNHibernate.Mapping;
using DaCollector.Server.Models.DaCollector;

namespace DaCollector.Server.Mappings;

public class DaCollectorManagedFolderMap : ClassMap<DaCollectorManagedFolder>
{
    public DaCollectorManagedFolderMap()
    {
        Table("ImportFolder");
        Not.LazyLoad();
        Id(x => x.ID).Column("ImportFolderID");

        Map(x => x.Path).Column("ImportFolderLocation").Not.Nullable();
        Map(x => x.Name).Column("ImportFolderName").Not.Nullable();
        Map(x => x.IsDropDestination).Not.Nullable();
        Map(x => x.IsDropSource).Not.Nullable();
        Map(x => x.IsWatched).Not.Nullable();
    }
}
