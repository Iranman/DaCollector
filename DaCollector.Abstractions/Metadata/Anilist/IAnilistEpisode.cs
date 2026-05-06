using DaCollector.Abstractions.Metadata.Containers;

namespace DaCollector.Abstractions.Metadata.Anilist;

/// <summary>
/// An AniList episode.
/// </summary>
public interface IAnilistEpisode : IEpisode, IWithCreationDate, IWithUpdateDate { }
