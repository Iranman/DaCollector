using System.Collections.Generic;
using System.Linq;
using DaCollector.Abstractions.Metadata.Enums;
using DaCollector.Abstractions.Metadata;
using DaCollector.Abstractions.Metadata.Containers;
using DaCollector.Abstractions.Metadata.DaCollector;
using DaCollector.Abstractions.Metadata.Tmdb;
using DaCollector.Abstractions.Metadata.Tmdb.CrossReferences;
using DaCollector.Server.Models.AniDB;
using DaCollector.Server.Models.DaCollector;
using DaCollector.Server.Models.TMDB;
using DaCollector.Server.Repositories;

#nullable enable
namespace DaCollector.Server.Models.CrossReference;

public class CrossRef_AniDB_TMDB_Show : ITmdbShowCrossReference
{
    #region Database Columns

    public int CrossRef_AniDB_TMDB_ShowID { get; set; }

    public int AnidbAnimeID { get; set; }

    public int TmdbShowID { get; set; }

    public MatchRating MatchRating { get; set; }

    #endregion
    #region Constructors

    public CrossRef_AniDB_TMDB_Show() { }

    public CrossRef_AniDB_TMDB_Show(int anidbAnimeId, int tmdbShowId, MatchRating matchRating = MatchRating.UserVerified)
    {
        AnidbAnimeID = anidbAnimeId;
        TmdbShowID = tmdbShowId;
        MatchRating = matchRating;
    }

    #endregion
    #region Methods

    public AniDB_Anime? AnidbAnime =>
        RepoFactory.AniDB_Anime.GetByAnimeID(AnidbAnimeID);

    public AnimeSeries? AnimeSeries =>
        RepoFactory.AnimeSeries.GetByAnimeID(AnidbAnimeID);

    public TMDB_Show? TmdbShow =>
        RepoFactory.TMDB_Show.GetByTmdbShowID(TmdbShowID);

    /// <summary>
    /// Get all images for the show, or all images for the given
    /// <paramref name="entityType"/> provided for the show.
    /// </summary>
    /// <param name="entityType">If set, will restrict the returned list to only
    /// containing the images of the given entity type.</param>
    /// <returns>A read-only list of images that are linked to the show.
    /// </returns>
    public IReadOnlyList<TMDB_Image> GetImages(ImageEntityType? entityType = null) => entityType.HasValue
        ? RepoFactory.TMDB_Image.GetByTmdbShowIDAndType(TmdbShowID, entityType.Value)
        : RepoFactory.TMDB_Image.GetByTmdbShowID(TmdbShowID);

    /// <summary>
    /// Get all images for the show, or all images for the given
    /// <paramref name="entityType"/> provided for the show.
    /// </summary>
    /// <param name="entityType">If set, will restrict the returned list to only
    /// containing the images of the given entity type.</param>
    /// <param name="preferredImages">The preferred images.</param>
    /// <returns>A read-only list of images that are linked to the show.
    /// </returns>
    public IReadOnlyList<IImage> GetImages(ImageEntityType? entityType, IReadOnlyDictionary<ImageEntityType, IImage> preferredImages) =>
        GetImages(entityType)
            .GroupBy(i => i.ImageType)
            .SelectMany(gB => preferredImages.TryGetValue(gB.Key, out var pI) ? gB.Select(i => i.Equals(pI) ? pI : i) : gB)
            .ToList();

    #endregion

    #region IWithImages Implementation

    IImage? IWithImages.GetPreferredImageForType(ImageEntityType entityType)
        => null;

    IReadOnlyList<IImage> IWithImages.GetImages(ImageEntityType? entityType)
        => GetImages(entityType);

    #endregion

    #region ITmdbShowCrossReference Implementation

    IDaCollectorSeries? ITmdbShowCrossReference.DaCollectorSeries => AnimeSeries;

    ITmdbShow? ITmdbShowCrossReference.TmdbShow => TmdbShow;

    #endregion
}
