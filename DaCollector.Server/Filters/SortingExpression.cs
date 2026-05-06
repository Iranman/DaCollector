using DaCollector.Abstractions.Filtering;

#nullable enable
namespace DaCollector.Server.Filters;

public abstract class SortingExpression : FilterExpression<object>, ISortingExpression
{
    public bool Descending { get; set; } // take advantage of default(bool) being false

    public SortingExpression? Next { get; set; }

    #region ISortingExpression Implementation

    ISortingExpression? ISortingExpression.Next => Next;

    #endregion
}
