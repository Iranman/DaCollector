using FluentNHibernate.Mapping;
using DaCollector.Server.Models.AniDB;
using DaCollector.Server.Server;

namespace DaCollector.Server.Mappings;

public class AniDB_Anime_StaffMap : ClassMap<AniDB_Anime_Staff>
{
    public AniDB_Anime_StaffMap()
    {
        Table("AniDB_Anime_Staff");
        Not.LazyLoad();
        Id(x => x.AniDB_Anime_StaffID);
        Map(x => x.AnimeID).Not.Nullable();
        Map(x => x.CreatorID).Not.Nullable();
        Map(x => x.RoleType).CustomType<CreatorRoleType>().Not.Nullable();
        Map(x => x.Role).Not.Nullable();
        Map(x => x.Ordering).Not.Nullable();
    }
}
