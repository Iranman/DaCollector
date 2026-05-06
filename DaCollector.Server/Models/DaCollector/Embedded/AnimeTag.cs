using System.Collections.Generic;
using System.Linq;
using DaCollector.Abstractions.Metadata.Enums;
using DaCollector.Abstractions.Extensions;
using DaCollector.Abstractions.Metadata.DaCollector;
using DaCollector.Server.Repositories;

namespace DaCollector.Server.Models.DaCollector.Embedded;

public class AnimeTag(CustomTag tag, AnimeSeries series) : IDaCollectorTagForSeries
{
    #region IMetadata Implementation

    public int ID => tag.CustomTagID;

    public DataSource Source => DataSource.User;

    #endregion

    #region ITag Implementation

    public string Name => tag.TagName;

    public string Description => tag.TagDescription;

    #endregion

    #region IDaCollectorTag Implementation

    public IReadOnlyList<IDaCollectorSeries> AllDaCollectorSeries => RepoFactory.CrossRef_CustomTag.GetByCustomTagID(tag.CustomTagID)
        .Select(xref => RepoFactory.AnimeSeries.GetByAnimeID(xref.CrossRefID))
        .WhereNotNull()
        .ToList();

    #endregion

    #region IDaCollectorTagForSeries Implementation

    public int DaCollectorSeriesID => series.AnimeSeriesID;

    public IDaCollectorSeries DaCollectorSeries => series;

    #endregion
}
