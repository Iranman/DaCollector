using System;

namespace DaCollector.Server.Filters.Interfaces;

public interface IWithSecondDateSelectorParameter
{
    FilterExpression<DateTime?> Right { get; set; }
}
