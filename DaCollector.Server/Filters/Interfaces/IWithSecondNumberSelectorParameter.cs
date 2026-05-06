namespace DaCollector.Server.Filters.Interfaces;

public interface IWithSecondNumberSelectorParameter
{
    FilterExpression<double> Right { get; set; }
}
