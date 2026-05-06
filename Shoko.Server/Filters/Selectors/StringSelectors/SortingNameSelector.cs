using System;
using Shoko.Abstractions.Filtering;

namespace Shoko.Server.Filters.Selectors.StringSelectors;

// TODO: REMOVE THIS FILTER EXPRESSION SOMETIME IN THE FUTURE AFTER THE LEGACY FILTERS ARE REMOVED!!1!
public class SortingNameSelector : FilterExpression<string>
{
    public override string HelpDescription => "This returns the sorting name of a filterable";
    public override FilterExpressionGroup Group => FilterExpressionGroup.Selector;

    public override string Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? time)
    {
        return filterable.SortName;
    }

    protected bool Equals(SortingNameSelector other)
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

        return Equals((SortingNameSelector)obj);
    }

    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }

    public static bool operator ==(SortingNameSelector left, SortingNameSelector right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(SortingNameSelector left, SortingNameSelector right)
    {
        return !Equals(left, right);
    }
}
