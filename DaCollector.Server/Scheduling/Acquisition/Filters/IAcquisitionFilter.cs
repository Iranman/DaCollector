using System;
using System.Collections.Generic;

namespace DaCollector.Server.Scheduling.Acquisition.Filters;

public interface IAcquisitionFilter
{
    IEnumerable<Type> GetTypesToExclude();
    event EventHandler StateChanged;
}
