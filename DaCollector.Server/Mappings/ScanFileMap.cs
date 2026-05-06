using FluentNHibernate.Mapping;
using DaCollector.Server.Models.Legacy;
using DaCollector.Server.Server;

namespace DaCollector.Server.Mappings;

public class ScanFileMap : ClassMap<ScanFile>
{
    public ScanFileMap()
    {
        Table("ScanFile");

        Not.LazyLoad();
        Id(x => x.ScanFileID);
        Map(x => x.ScanID).Not.Nullable();
        Map(x => x.ImportFolderID).Not.Nullable();
        Map(x => x.VideoLocal_Place_ID).Not.Nullable();
        Map(x => x.FullName).Not.Nullable();
        Map(x => x.FileSize).Not.Nullable();
        Map(x => x.Status).Not.Nullable().CustomType<ScanFileStatus>();
        Map(x => x.CheckDate).Nullable();
        Map(x => x.Hash).Not.Nullable();
        Map(x => x.HashResult).Nullable();
    }
}
