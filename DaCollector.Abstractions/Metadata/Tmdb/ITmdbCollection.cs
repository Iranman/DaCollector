using DaCollector.Abstractions.Metadata.Containers;

namespace DaCollector.Abstractions.Metadata.Tmdb;

/// <summary>
/// A TMDB collection.
/// </summary>
public interface ITmdbCollection : ICollection, IWithImages, IWithCreationDate, IWithUpdateDate { }
