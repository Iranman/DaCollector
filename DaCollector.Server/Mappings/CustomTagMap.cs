using FluentNHibernate.Mapping;
using DaCollector.Server.Models.DaCollector;

namespace DaCollector.Server.Mappings;

public class CustomTagMap : ClassMap<CustomTag>
{
    public CustomTagMap()
    {
        Not.LazyLoad();
        Id(x => x.CustomTagID);

        Map(x => x.TagName);
        Map(x => x.TagDescription);
    }
}
