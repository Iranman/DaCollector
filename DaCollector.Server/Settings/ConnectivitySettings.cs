using System.Collections.Generic;
using DaCollector.Abstractions.Config.Attributes;
using DaCollector.Abstractions.Config.Enums;

#nullable enable
namespace DaCollector.Server.Settings;

public class ConnectivitySettings
{
    /// <summary>
    /// The list of connectivity monitor definitions used for WAN availability checks.
    /// When <c>null</c>, the built-in defaults are used.
    /// </summary>
    [List(ListType = DisplayListType.ComplexInline)]
    public List<ConnectivityMonitorDefinition>? MonitorDefinitions { get; set; }
}
