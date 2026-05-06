
using System;
using System.Collections.Generic;
using DaCollector.Abstractions.Filtering;

namespace DaCollector.Server.Filters.Selectors.StringSetSelectors;

public class SeriesIDsSelector : FilterExpression<IReadOnlySet<string>>
{

    public override string HelpDescription => "This returns a set of all series IDs within the filterable.";
    public override FilterExpressionGroup Group => FilterExpressionGroup.Selector;

    public override IReadOnlySet<string> Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? time)
    {
        return filterable.SeriesIDs;
    }

    protected bool Equals(SeriesIDsSelector other)
    {
        return base.Equals(other);
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj))
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (obj.GetType() != this.GetType())
        {
            return false;
        }

        return Equals((SeriesIDsSelector)obj);
    }

    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }

    public static bool operator ==(SeriesIDsSelector left, SeriesIDsSelector right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(SeriesIDsSelector left, SeriesIDsSelector right)
    {
        return !Equals(left, right);
    }
}
