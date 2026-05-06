using System;
using DaCollector.Abstractions.Filtering;

namespace DaCollector.Server.Filters.Selectors.StringSelectors;

public class OriginalNameSelector : FilterExpression<string>
{
    public override string HelpDescription => "This returns the original name of a filterable";
    public override FilterExpressionGroup Group => FilterExpressionGroup.Selector;

    public override string Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? time)
    {
        return filterable.OriginalName;
    }

    protected bool Equals(OriginalNameSelector other)
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

        return Equals((OriginalNameSelector)obj);
    }

    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }

    public static bool operator ==(OriginalNameSelector left, OriginalNameSelector right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(OriginalNameSelector left, OriginalNameSelector right)
    {
        return !Equals(left, right);
    }
}
