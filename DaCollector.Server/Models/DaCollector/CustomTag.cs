using System.Collections.Generic;
using System.Linq;
using DaCollector.Abstractions.Metadata.Enums;
using DaCollector.Abstractions.Extensions;
using DaCollector.Abstractions.Metadata;
using DaCollector.Abstractions.Metadata.DaCollector;
using DaCollector.Server.Repositories;

#nullable enable
namespace DaCollector.Server.Models.DaCollector;

public class CustomTag : IDaCollectorTag
{
    public int CustomTagID { get; set; }

    public string TagName { get; set; } = string.Empty;

    public string TagDescription { get; set; } = string.Empty;

    #region IMetadata Implementation

    DataSource IMetadata.Source => DataSource.User;

    int IMetadata<int>.ID => CustomTagID;

    #endregion

    #region ITag Implementation

    string ITag.Name => TagName;

    string ITag.Description => TagDescription;

    #endregion

    #region IDaCollectorTag Implementation

    IReadOnlyList<IDaCollectorSeries> IDaCollectorTag.AllDaCollectorSeries => RepoFactory.CrossRef_CustomTag.GetByCustomTagID(CustomTagID)
        .Select(xref => RepoFactory.MediaSeries.GetByAnimeID(xref.CrossRefID))
        .WhereNotNull()
        .ToList();

    #endregion
}
