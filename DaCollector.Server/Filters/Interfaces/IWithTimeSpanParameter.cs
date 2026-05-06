using System;

namespace DaCollector.Server.Filters.Interfaces;

public interface IWithTimeSpanParameter
{
    TimeSpan Parameter { get; set; }
}
