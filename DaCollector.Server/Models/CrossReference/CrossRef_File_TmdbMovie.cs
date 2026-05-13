using System;
using DaCollector.Server.Models.DaCollector;
using DaCollector.Server.Models.TMDB;
using DaCollector.Server.Repositories;

#nullable enable
namespace DaCollector.Server.Models.CrossReference;

public class CrossRef_File_TmdbMovie
{
    public int CrossRef_File_TmdbMovieID { get; set; }

    public int VideoLocalID { get; set; }

    public int TmdbMovieID { get; set; }

    public bool IsManuallyLinked { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public VideoLocal? VideoLocal => RepoFactory.VideoLocal.GetByID(VideoLocalID);

    public TMDB_Movie? TmdbMovie => RepoFactory.TMDB_Movie.GetByTmdbMovieID(TmdbMovieID);
}
