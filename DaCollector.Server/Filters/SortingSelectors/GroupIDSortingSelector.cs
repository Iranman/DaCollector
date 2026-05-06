using System;
using DaCollector.Abstractions.Filtering;

namespace DaCollector.Server.Filters.SortingSelectors;

public class GroupIDSortingSelector : SortingExpression
{
    public override string HelpDescription => "This sorts by a filterable's closest group's ID.";

    public override object Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? time)
    {
        return filterable.GroupID;
    }
}
