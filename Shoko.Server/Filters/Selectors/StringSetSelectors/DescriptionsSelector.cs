
using System;
using System.Collections.Generic;
using Shoko.Abstractions.Filtering;

namespace Shoko.Server.Filters.Selectors.StringSetSelectors;

public class DescriptionsSelector : FilterExpression<IReadOnlySet<string>>
{

    public override string HelpDescription => "This returns a set of all descriptions within the filterable.";
    public override FilterExpressionGroup Group => FilterExpressionGroup.Selector;

    public override IReadOnlySet<string> Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? time)
    {
        return filterable.Descriptions;
    }

    protected bool Equals(DescriptionsSelector other)
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

        return Equals((DescriptionsSelector)obj);
    }

    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }

    public static bool operator ==(DescriptionsSelector left, DescriptionsSelector right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(DescriptionsSelector left, DescriptionsSelector right)
    {
        return !Equals(left, right);
    }
}
