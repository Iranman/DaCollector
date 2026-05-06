using System.Collections.Generic;

namespace DaCollector.Server.Filters.Interfaces;

public interface IWithStringSetSelectorParameter
{
    FilterExpression<IReadOnlySet<string>> Left { get; set; }
}
