using System;
using Shoko.Abstractions.Filtering;

namespace Shoko.Server.Filters.Selectors.StringSelectors;

public class MainNameSelector : FilterExpression<string>
{
    public override string HelpDescription => "This returns the main name of a filterable";
    public override FilterExpressionGroup Group => FilterExpressionGroup.Selector;

    public override string Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? time)
    {
        return filterable.MainName;
    }

    protected bool Equals(MainNameSelector other)
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

        return Equals((MainNameSelector)obj);
    }

    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }

    public static bool operator ==(MainNameSelector left, MainNameSelector right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(MainNameSelector left, MainNameSelector right)
    {
        return !Equals(left, right);
    }
}
