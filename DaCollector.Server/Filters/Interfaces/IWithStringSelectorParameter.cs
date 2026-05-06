namespace DaCollector.Server.Filters.Interfaces;

public interface IWithStringSelectorParameter
{
    FilterExpression<string> Left { get; set; }
}
