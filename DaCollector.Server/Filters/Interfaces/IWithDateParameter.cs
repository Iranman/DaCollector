using System;

namespace DaCollector.Server.Filters.Interfaces;

public interface IWithDateParameter
{
    DateTime Parameter { get; set; }
}
