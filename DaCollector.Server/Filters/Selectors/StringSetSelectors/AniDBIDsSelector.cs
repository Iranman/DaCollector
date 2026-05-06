using System;
using System.Collections.Generic;
using DaCollector.Abstractions.Filtering;

namespace DaCollector.Server.Filters.Selectors.StringSetSelectors;

// TODO: REMOVE THIS FILTER EXPRESSION SOMETIME IN THE FUTURE AFTER THE LEGACY FILTERS ARE REMOVED!!1!
public class AniDBIDsSelector : FilterExpression<IReadOnlySet<string>>
{
    public override string HelpDescription => "This returns a set of all the AniDB IDs in a filterable. Legacy alias for AnidbAnimeIDsSelector";

    public override FilterExpressionGroup Group => FilterExpressionGroup.Selector;

    public override IReadOnlySet<string> Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? time)
    {
        return filterable.AnidbAnimeIDs;
    }

    protected bool Equals(AniDBIDsSelector other)
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

        return Equals((AniDBIDsSelector)obj);
    }

    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }

    public static bool operator ==(AniDBIDsSelector left, AniDBIDsSelector right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(AniDBIDsSelector left, AniDBIDsSelector right)
    {
        return !Equals(left, right);
    }
}
