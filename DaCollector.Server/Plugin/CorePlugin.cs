using System;
using DaCollector.Abstractions.Plugin;
using DaCollector.Abstractions.Utilities;

#nullable enable
namespace DaCollector.Server.Plugin;

/// <summary>
/// The core plugin. Responsible for allowing the core to register plugin
/// providers. You cannot disable this "plugin."
/// </summary>
public class CorePlugin : IPlugin
{
    /// <inheritdoc/>
    public Guid ID { get => UuidUtility.GetV5(GetType().FullName!); }

    /// <inheritdoc/>
    public string Name { get; private init; } = "DaCollector Core";

    public string Description { get; private init; } = """
        The core plugin. Responsible for allowing the core to register plugin
        providers. You cannot disable this "plugin."
    """;
}
