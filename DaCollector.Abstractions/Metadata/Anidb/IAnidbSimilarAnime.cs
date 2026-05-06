
namespace DaCollector.Abstractions.Metadata.Anidb;

/// <summary>
/// Similar AniDB anime.
/// </summary>
public interface IAnidbSimilarAnime
{
    /// <summary>
    /// The ID of the main AniDB anime.
    /// </summary>
    int BaseID { get; }

    /// <summary>
    /// The ID of the similar AniDB anime.
    /// </summary>
    int SimilarID { get; }

    /// <summary>
    ///   Overall user approval rating for the similarity match, normalized on a
    ///   scale of 0-100.
    /// </summary>
    double ApprovalRating { get; }

    /// <summary>
    ///   The number of votes in favor of the similarity match. Used to
    ///   calculate the <see cref="ApprovalRating" />.
    /// </summary>
    int ApprovalVotes { get; }

    /// <summary>
    ///   The total number of votes on the similarity match. Used to calculate
    ///   the <see cref="ApprovalRating" />.
    /// /// </summary>
    int TotalVotes { get; }

    /// <summary>
    /// The main AniDB anime.
    /// </summary>
    IAnidbAnime? Base { get; }

    /// <summary>
    /// The similar AniDB anime.
    /// </summary>
    IAnidbAnime? Similar { get; }
}
