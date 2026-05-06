using System;
using Shoko.Abstractions.Filtering;

namespace Shoko.Server.Filters.Selectors.StringSelectors;

public class DescriptionSelector : FilterExpression<string>
{
    public override string HelpDescription => "This returns the description of a filterable";
    public override FilterExpressionGroup Group => FilterExpressionGroup.Selector;

    public override string Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? time)
    {
        return filterable.Description;
    }

    protected bool Equals(DescriptionSelector other)
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

        return Equals((DescriptionSelector)obj);
    }

    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }

    public static bool operator ==(DescriptionSelector left, DescriptionSelector right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(DescriptionSelector left, DescriptionSelector right)
    {
        return !Equals(left, right);
    }
}
