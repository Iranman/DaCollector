namespace DaCollector.Server.Filters.Interfaces;

public interface IWithNumberSelectorParameter
{
    FilterExpression<double> Left { get; set; }
}
