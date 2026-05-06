using System;
using System.Linq;
using DaCollector.Abstractions.Filtering;
using DaCollector.Server.Filters.Interfaces;
using DaCollector.Server.Repositories;

namespace DaCollector.Server.Filters.Info;

public class HasReleaseGroupNameExpression : FilterExpression<bool>, IWithStringParameter
{
    public HasReleaseGroupNameExpression(string parameter)
    {
        Parameter = parameter;
    }

    public HasReleaseGroupNameExpression() { }

    public string Parameter { get; set; }
    public override bool TimeDependent => false;
    public override bool UserDependent => false;
    public override string HelpDescription => "This condition passes if any of the anime have the files of specified release group name";
    public override string[] HelpPossibleParameters => RepoFactory.StoredReleaseInfo.GetUsedReleaseGroups().Select(r => r.Name).ToArray();

    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? now)
    {
        return filterable.ReleaseGroupNames.Contains(Parameter);
    }

    protected bool Equals(HasReleaseGroupNameExpression other)
    {
        return base.Equals(other) && Parameter == other.Parameter;
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

        return Equals((HasReleaseGroupNameExpression)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Parameter);
    }
    public static bool operator ==(HasReleaseGroupNameExpression left, HasReleaseGroupNameExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(HasReleaseGroupNameExpression left, HasReleaseGroupNameExpression right)
    {
        return !Equals(left, right);
    }
}
