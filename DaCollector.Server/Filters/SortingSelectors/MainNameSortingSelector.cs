using System;
using DaCollector.Abstractions.Filtering;

namespace DaCollector.Server.Filters.SortingSelectors;

public class MainNameSortingSelector : SortingExpression
{
    public override string HelpDescription => "This sorts by a filterable's main name.";

    public override object Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? time)
    {
        return filterable.MainName;
    }
}
