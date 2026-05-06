using FluentNHibernate.Mapping;
using DaCollector.Server.Models.AniDB;
using DaCollector.Server.Server;

namespace DaCollector.Server.Mappings;

public class AniDB_NotifyQueueMap : ClassMap<AniDB_NotifyQueue>
{
    public AniDB_NotifyQueueMap()
    {
        Table("AniDB_NotifyQueue");
        Not.LazyLoad();
        Id(x => x.AniDB_NotifyQueueID);

        Map(x => x.Type).Not.Nullable().CustomType<AniDBNotifyType>();
        Map(x => x.ID).Not.Nullable();
        Map(x => x.AddedAt).Not.Nullable();
    }
}
