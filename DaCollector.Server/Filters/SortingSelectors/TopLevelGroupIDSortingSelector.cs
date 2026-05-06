using System;
using DaCollector.Abstractions.Filtering;

namespace DaCollector.Server.Filters.SortingSelectors;

public class TopLevelGroupIDSortingSelector : SortingExpression
{
    public override string HelpDescription => "This sorts by a filterable's top level group's ID.";

    public override object Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? time)
    {
        return filterable.TopLevelGroupID;
    }
}
