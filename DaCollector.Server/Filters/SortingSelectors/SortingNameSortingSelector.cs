using System;
using DaCollector.Abstractions.Filtering;

namespace DaCollector.Server.Filters.SortingSelectors;

// TODO: REMOVE THIS FILTER EXPRESSION SOMETIME IN THE FUTURE AFTER THE LEGACY FILTERS ARE REMOVED!!1!
public class SortingNameSortingSelector : SortingExpression
{
    public override string HelpDescription => "This sorts by a filterable's name, excluding common words like A, The, etc.";

    public override object Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? time)
    {
        return filterable.SortName;
    }
}
