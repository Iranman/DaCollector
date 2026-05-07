using System;
using System.Linq;
using DaCollector.Abstractions.Metadata.Enums;
using DaCollector.Abstractions.Filtering;
using DaCollector.Server.Filters.Interfaces;
using DaCollector.Server.Repositories;

namespace DaCollector.Server.Filters.Info;

public class HasAvailableImageExpression : FilterExpression<bool>, IWithStringParameter
{
    public HasAvailableImageExpression(string parameter)
    {
        if (Enum.TryParse<ImageEntityType>(parameter, out var imageEntityType))
            imageEntityType = ImageEntityType.None;
        Parameter = imageEntityType;
    }

    public HasAvailableImageExpression() { }

    public ImageEntityType Parameter { get; set; }
    public override string HelpDescription => "This condition passes if any of the anime has the available image type.";
    public override string[] HelpPossibleParameters => RepoFactory.MediaSeries.GetAllImageTypes().Select(a => a.ToString()).ToArray();

    string IWithStringParameter.Parameter
    {
        get => Parameter.ToString();
        set
        {
            if (Enum.TryParse<ImageEntityType>(value, out var imageEntityType))
                imageEntityType = ImageEntityType.None;
            Parameter = imageEntityType;
        }
    }

    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? time)
    {
        return filterable.AvailableImageTypes.Contains(Parameter);
    }

    protected bool Equals(HasAvailableImageExpression other)
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

        return Equals((HasAvailableImageExpression)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Parameter);
    }

    public static bool operator ==(HasAvailableImageExpression left, HasAvailableImageExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(HasAvailableImageExpression left, HasAvailableImageExpression right)
    {
        return !Equals(left, right);
    }
}
