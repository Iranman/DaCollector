using DaCollector.Abstractions.Metadata.Containers;

namespace DaCollector.Abstractions.Metadata;

/// <summary>
/// Collection metadata.
/// </summary>
public interface ICollection : IMetadata<string>, IWithTitles, IWithDescriptions { }
