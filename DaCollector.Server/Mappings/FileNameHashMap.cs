using FluentNHibernate.Mapping;
using DaCollector.Server.Models.DaCollector;

namespace DaCollector.Server.Mappings;

public class FileNameHashMap : ClassMap<FileNameHash>
{
    public FileNameHashMap()
    {
        Table("FileNameHash");
        Not.LazyLoad();
        Id(x => x.FileNameHashID);

        Map(x => x.Hash);
        Map(x => x.FileName);
        Map(x => x.FileSize).Not.Nullable();
        Map(x => x.DateTimeUpdated).Not.Nullable();
    }
}
