using FluentNHibernate.Mapping;
using DaCollector.Server.Models.AniDB;

namespace DaCollector.Server.Mappings;

public class AniDB_TagMap : ClassMap<AniDB_Tag>
{
    public AniDB_TagMap()
    {
        Table("AniDB_Tag");
        Not.LazyLoad();
        Id(x => x.AniDB_TagID);

        Map(x => x.TagID).Not.Nullable();
        Map(x => x.ParentTagID);
        Map(x => x.TagNameSource).Column("TagName").Not.Nullable();
        Map(x => x.TagNameOverride);
        Map(x => x.TagDescription).Not.Nullable().CustomType("StringClob");
        Map(x => x.GlobalSpoiler).Not.Nullable();
        Map(x => x.Verified).Not.Nullable();
        Map(x => x.LastUpdated);
    }
}
