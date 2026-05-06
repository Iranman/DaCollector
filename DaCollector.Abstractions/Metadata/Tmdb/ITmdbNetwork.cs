using System.Collections.Generic;
using DaCollector.Abstractions.Metadata.Containers;

namespace DaCollector.Abstractions.Metadata.Tmdb
{
    /// <summary>
    /// A TMDB network.
    /// </summary>
    public interface ITmdbNetwork : IMetadata<int>, IWithImages, IWithPortraitImage
    {
        /// <summary>
        /// Main name of the network on TMDB.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// The country the network originates from.
        /// </summary>
        string CountryOfOrigin { get; }

        /// <summary>
        /// The shows associated with this network, ordered by TMDB show id.
        /// </summary>
        IReadOnlyList<ITmdbShow> Shows { get; }
    }
}
