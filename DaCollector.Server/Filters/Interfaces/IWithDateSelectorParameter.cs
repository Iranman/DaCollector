using System;

namespace DaCollector.Server.Filters.Interfaces;

public interface IWithDateSelectorParameter
{
    FilterExpression<DateTime?> Left { get; set; }
}
