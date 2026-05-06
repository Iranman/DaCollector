using System;
using System.Collections.Generic;
using Shoko.Abstractions.Filtering;

namespace Shoko.Server.Filters.Selectors.StringSetSelectors;

public class ReleaseProviderNamesSelector : FilterExpression<IReadOnlySet<string>>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => false;
    public override string HelpDescription => "This returns a set of all release provider names in a filterable.";
    public override FilterExpressionGroup Group => FilterExpressionGroup.Selector;
    public override IReadOnlySet<string> Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? now)
    {
        return filterable.ReleaseProviderNames;
    }

    protected bool Equals(ReleaseProviderNamesSelector other)
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

        return Equals((ReleaseProviderNamesSelector)obj);
    }
    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }

    public static bool operator ==(ReleaseProviderNamesSelector left, ReleaseProviderNamesSelector right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(ReleaseProviderNamesSelector left, ReleaseProviderNamesSelector right)
    {
        return !Equals(left, right);
    }
}
