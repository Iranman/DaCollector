using System.Collections.Generic;
using System.Linq;
using DaCollector.Abstractions.Metadata.Enums;
using DaCollector.Abstractions.Extensions;
using DaCollector.Abstractions.Metadata;
using DaCollector.Abstractions.Metadata.Containers;
using DaCollector.Server.Models.Interfaces;
using DaCollector.Server.Repositories;
using DaCollector.Server.Server;

#nullable enable
namespace DaCollector.Server.Models.TMDB;

public class TMDB_Studio<TEntity> : IStudio<TEntity> where TEntity : IMetadata<int>, IEntityMetadata
{
    private readonly TMDB_Company _company;

    public int ID { get; private set; }

    public int ParentID { get; private set; }

    public string Name { get; private set; }

    public TEntity Parent { get; private set; }

    #region Constructor

    public TMDB_Studio(TMDB_Company company, TEntity parent)
    {
        _company = company;
        ID = company.TmdbCompanyID;
        ParentID = parent.ID;
        Name = company.Name;
        Parent = parent;
    }

    #endregion

    #region Methods

    IEnumerable<TMDB_Movie> GetMovies() =>
        RepoFactory.TMDB_Company_Entity.GetByTmdbEntityTypeAndCompanyID(ForeignEntityType.Movie, ID)
        .Select(xref => xref.GetTmdbMovie())
        .WhereNotNull();

    IEnumerable<TMDB_Show> GetShows() =>
        RepoFactory.TMDB_Company_Entity.GetByTmdbEntityTypeAndCompanyID(ForeignEntityType.Show, ID)
        .Select(xref => xref.GetTmdbShow())
        .WhereNotNull();

    IEnumerable<IMetadata> GetWorks() =>
        RepoFactory.TMDB_Company_Entity.GetByTmdbCompanyID(ID)
        .Select(xref => xref.GetTmdbEntity() as IMetadata<int>)
        .WhereNotNull();

    #endregion

    #region IMetadata Implementation

    DataSource IMetadata.Source => DataSource.TMDB;

    #endregion

    #region IWithPortraitImage Implementation

    IImage? IWithPortraitImage.PortraitImage =>
        _company.GetImages(ImageEntityType.Logo) is { Count: > 0 } images ? images[0] : null;

    #endregion

    #region IStudio Implementation

    string? IStudio.OriginalName => null;

    StudioType IStudio.StudioType => StudioType.None;

    IEnumerable<IMovie> IStudio.MovieWorks => GetMovies();

    IEnumerable<ISeries> IStudio.SeriesWorks => GetShows();

    IEnumerable<IMetadata> IStudio.Works => GetWorks();

    IMetadata<int>? IStudio.Parent => Parent;

    TEntity? IStudio<TEntity>.ParentOfType => Parent;

    #endregion
}
