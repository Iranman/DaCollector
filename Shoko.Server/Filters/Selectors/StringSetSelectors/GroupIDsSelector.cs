
using System;
using System.Collections.Generic;
using Shoko.Abstractions.Filtering;

namespace Shoko.Server.Filters.Selectors.StringSetSelectors;

public class GroupIDsSelector : FilterExpression<IReadOnlySet<string>>
{

    public override string HelpDescription => "This returns a set of all group IDs the filterable belongs to.";
    public override FilterExpressionGroup Group => FilterExpressionGroup.Selector;

    public override IReadOnlySet<string> Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? time)
    {
        return filterable.GroupIDs;
    }

    protected bool Equals(GroupIDsSelector other)
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

        return Equals((GroupIDsSelector)obj);
    }

    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }

    public static bool operator ==(GroupIDsSelector left, GroupIDsSelector right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(GroupIDsSelector left, GroupIDsSelector right)
    {
        return !Equals(left, right);
    }
}
