using System;
using Shoko.Abstractions.Filtering;

namespace Shoko.Server.Filters.SortingSelectors;

public class OriginalNameSortingSelector : SortingExpression
{
    public override string HelpDescription => "This sorts by a filterable's original name.";

    public override object Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? time)
    {
        return filterable.OriginalName;
    }
}
