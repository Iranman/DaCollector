using System;
using Shoko.Abstractions.Filtering;

namespace Shoko.Server.Filters.Selectors.NumberSelectors;

public class GroupIDSelector : FilterExpression<double>
{

    public override string HelpDescription => "This returns the filterable's closest group's ID";
    public override FilterExpressionGroup Group => FilterExpressionGroup.Selector;

    public override double Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? time)
    {
        return filterable.GroupID;
    }

    protected bool Equals(GroupIDSelector other)
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

        return Equals((GroupIDSelector)obj);
    }

    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }

    public static bool operator ==(GroupIDSelector left, GroupIDSelector right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(GroupIDSelector left, GroupIDSelector right)
    {
        return !Equals(left, right);
    }
}
