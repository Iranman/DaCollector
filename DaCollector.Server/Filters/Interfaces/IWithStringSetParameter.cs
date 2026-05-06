using System.Collections.Generic;

namespace DaCollector.Server.Filters.Interfaces;

public interface IWithStringSetParameter
{
    IReadOnlySet<string> Parameter { get; set; }
}
