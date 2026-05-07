using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using DaCollector.Abstractions.Metadata.Enums;
using DaCollector.Abstractions.Metadata;
using DaCollector.Abstractions.Metadata.DaCollector;
using DaCollector.Server.API.v3.Models.DaCollector;
using DaCollector.Server.Repositories;

namespace DaCollector.Server.API.v3.Models.Common;

/// <summary>
/// Describes relations between two series entries.
/// </summary>
public class SeriesRelation
{
    /// <summary>
    /// The IDs of the series.
    /// </summary>
    [Required]
    public RelationIDs IDs { get; set; }

    /// <summary>
    /// The IDs of the related series.
    /// </summary>
    [Required]
    public RelationIDs RelatedIDs { get; set; }

    /// <summary>
    /// The relation between <see cref="SeriesRelation.IDs"/> and <see cref="SeriesRelation.RelatedIDs"/>.
    /// </summary>
    [Required]
    [JsonConverter(typeof(StringEnumConverter))]
    public RelationType Type { get; set; }

    /// <summary>
    /// AniDB, etc.
    /// </summary>
    [Required]
    public string Source { get; set; }

    public SeriesRelation(IRelatedMetadata relation, IDaCollectorSeries series = null,
        IDaCollectorSeries relatedSeries = null)
    {
        series ??= RepoFactory.MediaSeries.GetByAnimeID(relation.BaseID);
        relatedSeries ??= RepoFactory.MediaSeries.GetByAnimeID(relation.RelatedID);

        IDs = new RelationIDs { AniDB = relation.BaseID, DaCollector = series?.ID };
        RelatedIDs = new RelationIDs { AniDB = relation.RelatedID, DaCollector = relatedSeries?.ID };
        Type = relation.RelationType;
        Source = "AniDB";
    }

    /// <summary>
    /// Relation IDs.
    /// </summary>
    public class RelationIDs
    {
        /// <summary>
        /// The ID of the <see cref="Series"/> entry.
        /// </summary>
        public int? DaCollector { get; set; }

        /// <summary>
        /// The ID of the <see cref="Series.AniDB"/> entry.
        /// </summary>
        public int? AniDB { get; set; }
    }
}
