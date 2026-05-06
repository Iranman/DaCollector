using System;
using System.Collections.Generic;
using Shoko.Abstractions.Filtering;

namespace Shoko.Server.Filters.Selectors.StringSetSelectors;

public class AnidbAnimeIDsSelector : FilterExpression<IReadOnlySet<string>>
{
    public override string HelpDescription => "This returns a set of all the AniDB IDs in a filterable.";

    public override FilterExpressionGroup Group => FilterExpressionGroup.Selector;

    public override IReadOnlySet<string> Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? time)
    {
        return filterable.AnidbAnimeIDs;
    }

    protected bool Equals(AnidbAnimeIDsSelector other)
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

        return Equals((AnidbAnimeIDsSelector)obj);
    }

    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }

    public static bool operator ==(AnidbAnimeIDsSelector left, AnidbAnimeIDsSelector right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(AnidbAnimeIDsSelector left, AnidbAnimeIDsSelector right)
    {
        return !Equals(left, right);
    }
}
