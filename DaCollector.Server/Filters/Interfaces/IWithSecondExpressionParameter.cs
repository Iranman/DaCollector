namespace DaCollector.Server.Filters.Interfaces;

public interface IWithSecondExpressionParameter
{
    FilterExpression<bool> Right { get; set; }
}
