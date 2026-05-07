using System;
using System.Collections.Generic;
using System.Linq;
using DaCollector.Abstractions.Metadata.Enums;
using DaCollector.Abstractions.Extensions;
using DaCollector.Abstractions.Metadata;
using DaCollector.Abstractions.Metadata.Anidb;
using DaCollector.Abstractions.Metadata.Anilist;
using DaCollector.Abstractions.Metadata.Anilist.CrossReferences;
using DaCollector.Abstractions.Metadata.Containers;
using DaCollector.Abstractions.Metadata.DaCollector;
using DaCollector.Abstractions.Metadata.Stub;
using DaCollector.Abstractions.Metadata.Tmdb;
using DaCollector.Abstractions.Metadata.Tmdb.CrossReferences;
using DaCollector.Abstractions.User;
using DaCollector.Abstractions.Video;
using DaCollector.Server.Extensions;
using DaCollector.Server.Models.AniDB;
using DaCollector.Server.Models.CrossReference;
using DaCollector.Server.Models.TMDB;
using DaCollector.Server.Providers.TMDB;
using DaCollector.Server.Repositories;
using DaCollector.Server.Utilities;

#nullable enable
namespace DaCollector.Server.Models.DaCollector;

public class MediaEpisode : IDaCollectorEpisode, IEquatable<MediaEpisode>
{
    #region DB Columns

    /// <summary>
    /// Local <see cref="MediaEpisode"/> id.
    /// </summary>
    public int MediaEpisodeID { get; set; }

    /// <summary>
    /// Local <see cref="DaCollector.MediaSeries"/> id.
    /// </summary>
    public int MediaSeriesID { get; set; }

    /// <summary>
    /// The universally unique anidb episode id.
    /// </summary>
    /// <remarks>
    /// Also see <seealso cref="AniDB.AniDB_Episode"/> for a local representation
    /// of the anidb episode data.
    /// </remarks>
    public int AniDB_EpisodeID { get; set; }

    /// <summary>
    /// Timestamp for when the entry was first created.
    /// </summary>
    public DateTime DateTimeCreated { get; set; }

    /// <summary>
    /// Timestamp for when the entry was last updated.
    /// </summary>
    public DateTime DateTimeUpdated { get; set; }

    /// <summary>
    /// Hidden episodes will not show up in the UI unless explicitly
    /// requested, and will also not count towards the unwatched count for
    /// the series.
    /// </summary>
    public bool IsHidden { get; set; }

    /// <summary>
    /// Episode name override.
    /// </summary>
    /// <value></value>
    public string? EpisodeNameOverride { get; set; }

    #endregion

    public EpisodeType EpisodeType => AniDB_Episode?.EpisodeType ?? EpisodeType.Episode;

    #region Titles

    public string Title => !string.IsNullOrEmpty(EpisodeNameOverride) ? EpisodeNameOverride : (PreferredTitle ?? DefaultTitle).Value;

    private ITitle? _defaultTitle;

    public ITitle DefaultTitle
    {
        get
        {
            if (_defaultTitle is not null)
                return _defaultTitle;

            lock (this)
            {
                if (_defaultTitle is not null)
                    return _defaultTitle;

                // Fallback to English if available.
                if (RepoFactory.AniDB_Episode_Title.GetByEpisodeIDAndLanguage(AniDB_EpisodeID, TitleLanguage.English) is { Count: > 0 } titles)
                    return _defaultTitle = titles[0];

                return _defaultTitle = new TitleStub()
                {
                    Language = TitleLanguage.Unknown,
                    LanguageCode = "unk",
                    Value = $"<AniDB Episode {AniDB_EpisodeID}>",
                    Source = DataSource.None,
                };
            }
        }
    }

    public ITitle? PreferredTitle
    {
        get
        {
            // Return the override if it's set.
            if (!string.IsNullOrEmpty(EpisodeNameOverride))
                return new TitleStub()
                {
                    Language = TitleLanguage.Unknown,
                    LanguageCode = "unk",
                    Value = EpisodeNameOverride,
                    Source = DataSource.User,
                    Type = TitleType.Main,
                };

            var settings = Utils.SettingsProvider.GetSettings();
            var sourceOrder = settings.Language.SeriesTitleSourceOrder;
            var languageOrder = Languages.PreferredEpisodeNamingLanguages;

            // Lazy load AniDB titles if needed.
            IReadOnlyList<AniDB_Episode_Title>? anidbTitles = null;
            IReadOnlyList<AniDB_Episode_Title> GetAnidbTitles()
                => anidbTitles ??= RepoFactory.AniDB_Episode_Title.GetByEpisodeID(AniDB_EpisodeID);

            // Lazy load TMDB titles if needed.
            IReadOnlyList<TMDB_Title>? tmdbTitles = null;
            IReadOnlyList<TMDB_Title> GetTmdbTitles()
                => tmdbTitles ??= (
                    TmdbEpisodes is { Count: > 0 } tmdbEpisodes
                        ? tmdbEpisodes[0].GetAllTitles()
                        : TmdbMovies is { Count: 1 } tmdbMovies
                            ? tmdbMovies[0].GetAllTitles()
                            : []
                );

            // Loop through all languages and sources, first by language, then by source.
            foreach (var language in languageOrder)
                foreach (var source in sourceOrder)
                {
                    ITitle? title = source switch
                    {
                        DataSource.AniDB =>
                            language.Language is TitleLanguage.Main
                                ? GetAnidbTitles().FirstOrDefault(x => x.Language is TitleLanguage.English)
                                : GetAnidbTitles().FirstOrDefault(x => x.Language == language.Language),
                        DataSource.TMDB =>
                            GetTmdbTitles().GetByLanguage(language.Language),
                        _ => null,
                    };
                    if (title is not null)
                        return title;
                }

            // The most "default" title we have, even if AniDB isn't a preferred source.
            return DefaultTitle;
        }
    }

    public IReadOnlyList<ITitle> Titles
    {
        get
        {
            var titles = new List<ITitle>();
            var episodeOverrideTitle = !string.IsNullOrEmpty(EpisodeNameOverride);
            if (episodeOverrideTitle)
            {
                titles.Add(new TitleStub()
                {
                    Source = DataSource.User,
                    Language = TitleLanguage.Unknown,
                    LanguageCode = "unk",
                    Value = EpisodeNameOverride!,
                    Type = TitleType.Main,
                });
            }

            var animeTitles = (this as IDaCollectorEpisode).MetadataEpisode.Titles.ToList();
            if (episodeOverrideTitle)
            {
                var mainTitle = animeTitles.Find(title => title.Type == TitleType.Main);
                if (mainTitle is not null)
                {
                    animeTitles.Remove(mainTitle);
                    animeTitles.Insert(0, new TitleStub()
                    {
                        Language = mainTitle.Language,
                        LanguageCode = mainTitle.LanguageCode,
                        Value = mainTitle.Value,
                        CountryCode = mainTitle.CountryCode,
                        Source = mainTitle.Source,
                        Type = TitleType.None,
                    });
                }
            }
            titles.AddRange(animeTitles);
            titles.AddRange((this as IDaCollectorEpisode).LinkedEpisodes.Where(e => e.Source is not DataSource.AniDB).SelectMany(ep => ep.Titles));

            return titles;
        }
    }

    public IText? PreferredOverview
    {
        get
        {
            var settings = Utils.SettingsProvider.GetSettings();
            var sourceOrder = settings.Language.DescriptionSourceOrder;
            var languageOrder = Languages.PreferredDescriptionNamingLanguages;
            var anidbOverview = AniDB_Episode?.Description;

            // Lazy load TMDB overviews if needed.
            IReadOnlyList<TMDB_Overview>? tmdbOverviews = null;
            IReadOnlyList<TMDB_Overview> GetTmdbOverviews()
                => tmdbOverviews ??= (
                    TmdbEpisodes is { Count: > 0 } tmdbEpisodes
                        ? tmdbEpisodes[0].GetAllOverviews()
                        : TmdbMovies is { Count: 1 } tmdbMovies
                            ? tmdbMovies[0].GetAllOverviews()
                            : []
                );

            // Check each language and source in the most preferred order.
            foreach (var language in languageOrder)
                foreach (var source in sourceOrder)
                {
                    IText? overview = source switch
                    {
                        DataSource.AniDB =>
                            language.Language is TitleLanguage.English && !string.IsNullOrEmpty(anidbOverview)
                                ? new TextStub()
                                {
                                    Language = TitleLanguage.English,
                                    LanguageCode = "en",
                                    Value = anidbOverview,
                                    Source = DataSource.AniDB,
                                }
                                : null,
                        DataSource.TMDB =>
                            GetTmdbOverviews().GetByLanguage(language.Language),
                        _ => null,
                    };
                    if (overview is not null)
                        return overview;
                }
            // Return nothing if no provider had an overview in the preferred language.
            return null;
        }
    }

    #endregion

    #region Images

    public IImage? GetPreferredImageForType(ImageEntityType entityType)
        => RepoFactory.AniDB_Episode_PreferredImage.GetByAnidbEpisodeIDAndType(AniDB_EpisodeID, entityType)?.GetImageMetadata();

    public IReadOnlyList<IImage> GetImages(ImageEntityType? entityType = null)
    {
        var preferredImages = (
                entityType.HasValue
                    ? [RepoFactory.AniDB_Episode_PreferredImage.GetByAnidbEpisodeIDAndType(AniDB_EpisodeID, entityType.Value)!]
                    : RepoFactory.AniDB_Episode_PreferredImage.GetByEpisodeID(AniDB_EpisodeID)
            )
            .WhereNotNull()
            .Select(preferredImage => preferredImage.GetImageMetadata())
            .WhereNotNull()
            .ToDictionary(image => image.ImageType);
        var images = new List<IImage>();
        if (!entityType.HasValue || entityType.Value is ImageEntityType.Poster)
        {
            var poster = AniDB_Anime?.GetImageMetadata(false);
            if (poster is not null)
                images.Add(preferredImages.TryGetValue(ImageEntityType.Poster, out var preferredPoster) && poster.Equals(preferredPoster)
                    ? preferredPoster
                    : poster
                );
        }
        foreach (var xref in TmdbEpisodeCrossReferences)
            images.AddRange(xref.GetImages(entityType, preferredImages));
        foreach (var xref in TmdbMovieCrossReferences)
            images.AddRange(xref.GetImages(entityType, preferredImages));

        return images
            .DistinctBy(image => (image.ImageType, image.Source, image.ID))
            .ToList();
    }

    #endregion

    #region DaCollector

    public MediaEpisode_User? GetUserRecord(int userID)
        => userID <= 0 ? null : RepoFactory.MediaEpisode_User.GetByUserAndEpisodeID(userID, MediaEpisodeID);

    /// <summary>
    /// Gets the MediaSeries this episode belongs to
    /// </summary>
    public MediaSeries? MediaSeries
        => RepoFactory.MediaSeries.GetByID(MediaSeriesID);

    public IReadOnlyList<VideoLocal> VideoLocals
        => RepoFactory.VideoLocal.GetByAniDBEpisodeID(AniDB_EpisodeID);

    public IReadOnlyList<CrossRef_File_Episode> FileCrossReferences
        => RepoFactory.CrossRef_File_Episode.GetByEpisodeID(AniDB_EpisodeID);

    #endregion

    #region AniDB

    public AniDB_Episode? AniDB_Episode => RepoFactory.AniDB_Episode.GetByEpisodeID(AniDB_EpisodeID);

    public AniDB_Anime? AniDB_Anime => AniDB_Episode?.AniDB_Anime;

    #endregion

    #region TMDB

    public IReadOnlyList<CrossRef_AniDB_TMDB_Movie> TmdbMovieCrossReferences =>
        RepoFactory.CrossRef_AniDB_TMDB_Movie.GetByAnidbEpisodeID(AniDB_EpisodeID);

    public IReadOnlyList<TMDB_Movie> TmdbMovies =>
        TmdbMovieCrossReferences
            .Select(xref => xref.TmdbMovie)
            .WhereNotNull()
            .ToList();

    public IReadOnlyList<CrossRef_AniDB_TMDB_Episode> TmdbEpisodeCrossReferences =>
        RepoFactory.CrossRef_AniDB_TMDB_Episode.GetByAnidbEpisodeID(AniDB_EpisodeID);

    public IReadOnlyList<TMDB_Episode> TmdbEpisodes =>
        TmdbEpisodeCrossReferences
            .Select(xref => xref.TmdbEpisode)
            .WhereNotNull()
            .ToList();

    #endregion

    public bool Equals(MediaEpisode? other)
        => other is not null &&
            MediaEpisodeID == other.MediaEpisodeID &&
            MediaSeriesID == other.MediaSeriesID &&
            AniDB_EpisodeID == other.AniDB_EpisodeID &&
            DateTimeUpdated == other.DateTimeUpdated &&
            DateTimeCreated == other.DateTimeCreated;

    public override bool Equals(object? obj)
        => obj is not null && (ReferenceEquals(this, obj) || (obj is MediaEpisode ep && Equals(ep)));

    public override int GetHashCode()
        => HashCode.Combine(MediaEpisodeID, MediaSeriesID, AniDB_EpisodeID, DateTimeUpdated, DateTimeCreated);

    #region IMetadata Implementation

    DataSource IMetadata.Source => DataSource.DaCollector;

    int IMetadata<int>.ID => MediaEpisodeID;

    #endregion

    #region IWithDescription Implementation

    IText? IWithDescriptions.DefaultDescription => AniDB_Episode?.Description is { Length: > 0 }
        ? new TextStub()
        {
            Language = TitleLanguage.English,
            LanguageCode = "en",
            Value = AniDB_Episode.Description,
            Source = DataSource.AniDB,
        }
        : null;

    IText? IWithDescriptions.PreferredDescription => PreferredOverview;

    IReadOnlyList<IText> IWithDescriptions.Descriptions => (this as IDaCollectorEpisode).LinkedEpisodes.SelectMany(ep => ep.Descriptions).ToList();

    #endregion

    #region IWithCreationDate Implementation

    DateTime IWithCreationDate.CreatedAt => DateTimeCreated.ToUniversalTime();

    #endregion

    #region IWithUpdateDate Implementation

    DateTime IWithUpdateDate.LastUpdatedAt => DateTimeUpdated.ToUniversalTime();

    #endregion

    #region IWithCastAndCrew Implementation

    IReadOnlyList<ICast> IWithCastAndCrew.Cast
    {
        get
        {
            var list = new List<ICast>();
            if (AniDB_Episode is IEpisode MetadataEpisode)
                list.AddRange(MetadataEpisode.Cast);
            foreach (var movie in TmdbMovies)
                list.AddRange(movie.Cast);
            foreach (var episode in TmdbEpisodes)
                list.AddRange(episode.Cast);
            return list;
        }
    }

    IReadOnlyList<ICrew> IWithCastAndCrew.Crew
    {
        get
        {
            var list = new List<ICrew>();
            if (AniDB_Episode is IEpisode MetadataEpisode)
                list.AddRange(MetadataEpisode.Crew);
            foreach (var movie in TmdbMovies)
                list.AddRange(movie.Crew);
            foreach (var episode in TmdbEpisodes)
                list.AddRange(episode.Crew);
            return list;
        }
    }

    #endregion

    #region IEpisode Implementation

    int IEpisode.SeriesID => MediaSeriesID;

    IReadOnlyList<int> IEpisode.DaCollectorEpisodeIDs => [MediaEpisodeID];

    EpisodeType IEpisode.Type => EpisodeType;

    int IEpisode.EpisodeNumber => AniDB_Episode?.EpisodeNumber ?? 1;

    int? IEpisode.SeasonNumber => EpisodeType switch { EpisodeType.Episode => 1, EpisodeType.Special => 0, _ => null };

    double IEpisode.Rating => AniDB_Episode?.RatingDouble ?? 0;

    int IEpisode.RatingVotes => AniDB_Episode?.VotesInt ?? 0;

    IImage? IEpisode.DefaultThumbnail => TmdbEpisodes is { Count: > 0 } tmdbEpisodes
        ? tmdbEpisodes[0].DefaultThumbnail
        : null;

    TimeSpan IEpisode.Runtime => TimeSpan.FromSeconds(AniDB_Episode?.LengthSeconds ?? 0);

    DateOnly? IEpisode.AirDate
    {
        get
        {
            // TODO: Add AniList Episode air date check here

            if (AniDB_Episode?.GetAirDateAsDateOnly() is { } airDate)
                return airDate;

            foreach (var xref in TmdbEpisodeCrossReferences)
            {
                if (xref.TmdbEpisode?.AiredAt is { } tmdbAirDate)
                    return tmdbAirDate;
            }

            return null;
        }
    }

    DateTime? IEpisode.AirDateWithTime
    {
        get
        {
            // TODO: Add AniList Episode air date check here

            if (AniDB_Episode?.GetAirDateAsDate() is { } airDate)
                return airDate;

            foreach (var xref in TmdbEpisodeCrossReferences)
            {
                if (xref.TmdbEpisode?.AiredAt is { } tmdbAirDate)
                    return tmdbAirDate.ToDateTime().Date;
            }

            return null;
        }
    }

    ISeries? IEpisode.Series => MediaSeries;

    IReadOnlyList<IDaCollectorEpisode> IEpisode.DaCollectorEpisodes => [this];

    IReadOnlyList<IVideoCrossReference> IEpisode.CrossReferences =>
        RepoFactory.CrossRef_File_Episode.GetByEpisodeID(AniDB_EpisodeID);

    IReadOnlyList<IVideo> IEpisode.VideoList =>
        RepoFactory.CrossRef_File_Episode.GetByEpisodeID(AniDB_EpisodeID)
            .DistinctBy(xref => xref.Hash)
            .Select(xref => xref.VideoLocal)
            .WhereNotNull()
            .ToList();

    #endregion

    #region IDaCollectorEpisode Implementation

    int IDaCollectorEpisode.AnidbEpisodeID => AniDB_EpisodeID;

    IDaCollectorSeries? IDaCollectorEpisode.Series => MediaSeries;

    IAnidbEpisode IDaCollectorEpisode.MetadataEpisode => AniDB_Episode ??
        throw new NullReferenceException($"Unable to find AniDB Episode {AniDB_EpisodeID} for MediaEpisode {MediaEpisodeID}");

    IReadOnlyList<IAnilistEpisode> IDaCollectorEpisode.AnilistEpisodes => [];

    IReadOnlyList<IAnilistEpisodeCrossReference> IDaCollectorEpisode.AnilistEpisodeCrossReferences => [];

    IReadOnlyList<ITmdbEpisode> IDaCollectorEpisode.TmdbEpisodes => TmdbEpisodes;

    IReadOnlyList<IMovie> IDaCollectorEpisode.TmdbMovies => TmdbMovies;

    IReadOnlyList<ITmdbEpisodeCrossReference> IDaCollectorEpisode.TmdbEpisodeCrossReferences => TmdbEpisodeCrossReferences;

    IReadOnlyList<ITmdbMovieCrossReference> IDaCollectorEpisode.TmdbMovieCrossReferences => TmdbMovieCrossReferences;

    IReadOnlyList<IEpisode> IDaCollectorEpisode.LinkedEpisodes
    {
        get
        {
            var episodeList = new List<IEpisode>();

            var MetadataEpisode = AniDB_Episode;
            if (MetadataEpisode is not null)
                episodeList.Add(MetadataEpisode);

            episodeList.AddRange(TmdbEpisodes);

            // Add more episodes here as needed.

            return episodeList;
        }
    }

    IReadOnlyList<IMovie> IDaCollectorEpisode.LinkedMovies
    {
        get
        {
            var movieList = new List<IMovie>();

            movieList.AddRange(TmdbMovies);

            // Add more movies here as needed.

            return movieList;
        }
    }

    IEpisodeUserData IDaCollectorEpisode.GetUserData(IUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (user.ID is 0 || RepoFactory.JMMUser.GetByID(user.ID) is null)
            throw new ArgumentException("User is not stored in the database!", nameof(user));
        var userData = GetUserRecord(user.ID)
            ?? new() { JMMUserID = user.ID, MediaEpisodeID = MediaEpisodeID, MediaSeriesID = MediaSeriesID };
        if (userData.MediaEpisode_UserID is 0)
            RepoFactory.MediaEpisode_User.Save(userData);
        return userData;
    }

    #endregion
}
