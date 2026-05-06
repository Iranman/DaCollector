using FluentNHibernate.Mapping;
using DaCollector.Server.Models.DaCollector;

namespace DaCollector.Server.Mappings;

public class VideoLocal_PlaceMap : ClassMap<VideoLocal_Place>
{
    public VideoLocal_PlaceMap()
    {
        Table("VideoLocal_Place");
        Not.LazyLoad();
        Id(x => x.ID).Column("VideoLocal_Place_ID");

        Map(x => x.VideoID).Column("VideoLocalID").Not.Nullable();
        Map(x => x.ManagedFolderID).Column("ImportFolderID").Not.Nullable();
        Map(x => x.RelativePath).Column("FilePath").Not.Nullable();
    }
}
