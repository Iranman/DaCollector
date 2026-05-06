using System;
using DaCollector.Abstractions.Filtering;

namespace DaCollector.Server.Filters.Selectors.NumberSelectors;

public class TopLevelGroupIDSelector : FilterExpression<double>
{

    public override string HelpDescription => "This returns the filterable's top level group's ID";
    public override FilterExpressionGroup Group => FilterExpressionGroup.Selector;

    public override double Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? time)
    {
        return filterable.TopLevelGroupID;
    }

    protected bool Equals(TopLevelGroupIDSelector other)
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

        return Equals((TopLevelGroupIDSelector)obj);
    }

    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }

    public static bool operator ==(TopLevelGroupIDSelector left, TopLevelGroupIDSelector right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(TopLevelGroupIDSelector left, TopLevelGroupIDSelector right)
    {
        return !Equals(left, right);
    }
}
