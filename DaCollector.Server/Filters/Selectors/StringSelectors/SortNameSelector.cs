using System;
using DaCollector.Abstractions.Filtering;

namespace DaCollector.Server.Filters.Selectors.StringSelectors;

public class SortNameSelector : FilterExpression<string>
{
    public override string HelpDescription => "This returns the sorting name of a filterable";
    public override FilterExpressionGroup Group => FilterExpressionGroup.Selector;

    public override string Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? time)
    {
        return filterable.SortName;
    }

    protected bool Equals(SortNameSelector other)
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

        return Equals((SortNameSelector)obj);
    }

    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }

    public static bool operator ==(SortNameSelector left, SortNameSelector right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(SortNameSelector left, SortNameSelector right)
    {
        return !Equals(left, right);
    }
}
