using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
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
using DaCollector.Server.Models.DaCollector.Embedded;
using DaCollector.Server.Models.TMDB;
using DaCollector.Server.Models.TVDB;
using DaCollector.Server.Providers.AniDB.Titles;
using DaCollector.Server.Providers.TMDB;
using DaCollector.Server.Repositories;
using DaCollector.Server.Server;
using DaCollector.Server.Utilities;

#nullable enable
namespace DaCollector.Server.Models.DaCollector;

public class AnimeSeries : IDaCollectorSeries
{
    #region DB Columns

    public int AnimeSeriesID { get; set; }

    public int AnimeGroupID { get; set; }

    public int AniDB_ID { get; set; }

    public int? TVDB_ShowID { get; set; }

    public int? TVDB_MovieID { get; set; }

    public int? TMDB_ShowID { get; set; }

    public int? TMDB_MovieID { get; set; }

    public DateTime DateTimeUpdated { get; set; }

    public DateTime DateTimeCreated { get; set; }

    public string? DefaultAudioLanguage { get; set; }

    public string? DefaultSubtitleLanguage { get; set; }

    public DateTime? EpisodeAddedDate { get; set; }

    public DateTime? LatestEpisodeAirDate { get; set; }

    public DayOfWeek? AirsOn { get; set; }

    public int MissingEpisodeCount { get; set; }

    public int MissingEpisodeCountGroups { get; set; }

    public int HiddenMissingEpisodeCount { get; set; }

    public int HiddenMissingEpisodeCountGroups { get; set; }

    public int LatestLocalEpisodeNumber { get; set; }

    public string? SeriesNameOverride { get; set; }

    public DateTime UpdatedAt { get; set; }

    public DisabledAutoMatchFlag DisableAutoMatchFlags { get; set; } = 0;

    #endregion

    #region Disabled Auto Matching

    public bool IsTMDBAutoMatchingDisabled
    {
        get
        {
            return DisableAutoMatchFlags.HasFlag(DisabledAutoMatchFlag.TMDB);
        }
        set
        {
            if (value)
                DisableAutoMatchFlags |= DisabledAutoMatchFlag.TMDB;
            else
                DisableAutoMatchFlags &= ~DisabledAutoMatchFlag.TMDB;
        }
    }

    public bool IsMALAutoMatchingDisabled
    {
        get
        {
            return DisableAutoMatchFlags.HasFlag(DisabledAutoMatchFlag.MAL);
        }
        set
        {
            if (value)
                DisableAutoMatchFlags |= DisabledAutoMatchFlag.MAL;
            else
                DisableAutoMatchFlags &= ~DisabledAutoMatchFlag.MAL;
        }
    }

    public bool IsAniListAutoMatchingDisabled
    {
        get
        {
            return DisableAutoMatchFlags.HasFlag(DisabledAutoMatchFlag.AniList);
        }
        set
        {
            if (value)
                DisableAutoMatchFlags |= DisabledAutoMatchFlag.AniList;
            else
                DisableAutoMatchFlags &= ~DisabledAutoMatchFlag.AniList;
        }
    }

    public bool IsAnimeshonAutoMatchingDisabled
    {
        get
        {
            return DisableAutoMatchFlags.HasFlag(DisabledAutoMatchFlag.Animeshon);
        }
        set
        {
            if (value)
                DisableAutoMatchFlags |= DisabledAutoMatchFlag.Animeshon;
            else
                DisableAutoMatchFlags &= ~DisabledAutoMatchFlag.Animeshon;
        }
    }

    public bool IsKitsuAutoMatchingDisabled
    {
        get
        {
            return DisableAutoMatchFlags.HasFlag(DisabledAutoMatchFlag.Kitsu);
        }
        set
        {
            if (value)
                DisableAutoMatchFlags |= DisabledAutoMatchFlag.Kitsu;
            else
                DisableAutoMatchFlags &= ~DisabledAutoMatchFlag.Kitsu;
        }
    }

    #endregion

    #region Titles & Overviews

    public string Title => !string.IsNullOrEmpty(SeriesNameOverride) ? SeriesNameOverride : (PreferredTitle ?? DefaultTitle).Value;

    private ITitle? _defaultTitle = null;

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

                // Return the override if it's set.
                if (!string.IsNullOrEmpty(SeriesNameOverride))
                    return _defaultTitle = new TitleStub
                    {
                        Source = DataSource.User,
                        Language = TitleLanguage.Unknown,
                        LanguageCode = "unk",
                        Value = SeriesNameOverride,
                        Type = TitleType.Main,
                    };

                if (AniDB_Anime is { } anime)
                    return _defaultTitle = anime.DefaultTitle;

                if (TVDB_ShowID.HasValue && RepoFactory.TVDB_Show.GetByTvdbShowID(TVDB_ShowID.Value) is { } tvdbShow && !string.IsNullOrEmpty(tvdbShow.Name))
                    return _defaultTitle = new TitleStub { Language = TitleLanguage.English, LanguageCode = "en", Value = tvdbShow.Name, Source = DataSource.TvDB };

                if (TVDB_MovieID.HasValue && RepoFactory.TVDB_Movie.GetByTvdbMovieID(TVDB_MovieID.Value) is { } tvdbMovie && !string.IsNullOrEmpty(tvdbMovie.Name))
                    return _defaultTitle = new TitleStub { Language = TitleLanguage.English, LanguageCode = "en", Value = tvdbMovie.Name, Source = DataSource.TvDB };

                if (TMDB_ShowID.HasValue && RepoFactory.TMDB_Show.GetByTmdbShowID(TMDB_ShowID.Value) is { } tmdbShow && !string.IsNullOrEmpty(tmdbShow.EnglishTitle))
                    return _defaultTitle = new TitleStub { Language = TitleLanguage.English, LanguageCode = "en", Value = tmdbShow.EnglishTitle, Source = DataSource.TMDB };

                if (TMDB_MovieID.HasValue && RepoFactory.TMDB_Movie.GetByTmdbMovieID(TMDB_MovieID.Value) is { } tmdbMovie && !string.IsNullOrEmpty(tmdbMovie.EnglishTitle))
                    return _defaultTitle = new TitleStub { Language = TitleLanguage.English, LanguageCode = "en", Value = tmdbMovie.EnglishTitle, Source = DataSource.TMDB };

                if (AniDB_ID != 0)
                {
                    var titleHelper = Utils.ServiceContainer.GetRequiredService<AniDBTitleHelper>();
                    if (titleHelper.SearchAnimeID(AniDB_ID) is { } titleResponse && titleResponse.Titles.FirstOrDefault(title => title.TitleType == TitleType.Main) is { } defaultTitle)
                        return _defaultTitle = defaultTitle;
                }

                return _defaultTitle = new TitleStub()
                {
                    Language = TitleLanguage.Unknown,
                    LanguageCode = "unk",
                    Value = TVDB_ShowID.HasValue ? $"<TVDB Show {TVDB_ShowID}>"
                        : TVDB_MovieID.HasValue ? $"<TVDB Movie {TVDB_MovieID}>"
                        : TMDB_ShowID.HasValue ? $"<TMDB Show {TMDB_ShowID}>"
                        : TMDB_MovieID.HasValue ? $"<TMDB Movie {TMDB_MovieID}>"
                        : $"<Series {AnimeSeriesID}>",
                    Source = DataSource.None,
                };
            }
        }
    }

    private bool _preferredTitleLoaded = false;

    private ITitle? _preferredTitle = null;

    public ITitle? PreferredTitle => LoadPreferredTitle();

    public void ResetPreferredTitle()
    {
        lock (this)
        {
            if (_preferredTitleLoaded)
                return;
            _preferredTitleLoaded = false;
            _preferredTitle = null;
        }
        LoadPreferredTitle();
    }

    private ITitle? LoadPreferredTitle()
    {
        if (_preferredTitleLoaded)
            return _preferredTitle;

        lock (this)
        {
            if (_preferredTitleLoaded)
                return _preferredTitle;
            _preferredTitleLoaded = true;

            // Return the override if it's set.
            if (!string.IsNullOrEmpty(SeriesNameOverride))
                return _preferredTitle = new TitleStub
                {
                    Source = DataSource.User,
                    Language = TitleLanguage.Unknown,
                    LanguageCode = "unk",
                    Value = SeriesNameOverride,
                    Type = TitleType.Main,
                };

            var settings = Utils.SettingsProvider.GetSettings();
            var sourceOrder = settings.Language.SeriesTitleSourceOrder;
            var languageOrder = Languages.PreferredNamingLanguages;
            var anime = AniDB_Anime;

            // Lazy load AniDB titles if needed.
            List<AniDB_Anime_Title>? anidbTitles = null;
            List<AniDB_Anime_Title> GetAnidbTitles()
                => anidbTitles ??= RepoFactory.AniDB_Anime_Title.GetByAnimeID(AniDB_ID);

            // Lazy load TMDB titles if needed.
            IReadOnlyList<TMDB_Title>? tmdbTitles = null;

            // Loop through all languages and sources, first by language, then by source.
            foreach (var language in languageOrder)
                foreach (var source in sourceOrder)
                {
                    ITitle? title = source switch
                    {
                        DataSource.AniDB =>
                            language.Language is TitleLanguage.Main
                                ? anime?.DefaultTitle
                                : GetAnidbTitles().FirstOrDefault(x => x.TitleType is TitleType.Main or TitleType.Official && x.Language == language.Language)
                                    ?? (settings.Language.UseSynonyms ? GetAnidbTitles().FirstOrDefault(x => x.Language == language.Language) : null),
                        DataSource.TMDB =>
                            (tmdbTitles ??= GetTmdbTitles()).GetByLanguage(language.Language),
                        DataSource.TvDB =>
                            language.Language is TitleLanguage.Main or TitleLanguage.English or TitleLanguage.EnglishAmerican
                                ? GetTvdbTitle()
                                : null,
                        _ => null,
                    };
                    if (title is not null)
                        return _preferredTitle = title;
                }

            // The most "default" title we have, even if AniDB isn't a preferred source.
            return _preferredTitle = DefaultTitle;
        }
    }

    private ITitle? GetTvdbTitle()
    {
        if (TVDB_ShowID.HasValue && RepoFactory.TVDB_Show.GetByTvdbShowID(TVDB_ShowID.Value) is { } tvdbShow && !string.IsNullOrEmpty(tvdbShow.Name))
            return new TitleStub { Language = TitleLanguage.English, LanguageCode = "en", Value = tvdbShow.Name, Source = DataSource.TvDB };
        if (TVDB_MovieID.HasValue && RepoFactory.TVDB_Movie.GetByTvdbMovieID(TVDB_MovieID.Value) is { } tvdbMovie && !string.IsNullOrEmpty(tvdbMovie.Name))
            return new TitleStub { Language = TitleLanguage.English, LanguageCode = "en", Value = tvdbMovie.Name, Source = DataSource.TvDB };
        return null;
    }

    private IText? GetTvdbOverview()
    {
        string? overview = null;
        if (TVDB_ShowID.HasValue && RepoFactory.TVDB_Show.GetByTvdbShowID(TVDB_ShowID.Value) is { } tvdbShow && !string.IsNullOrEmpty(tvdbShow.Overview))
            overview = tvdbShow.Overview;
        else if (TVDB_MovieID.HasValue && RepoFactory.TVDB_Movie.GetByTvdbMovieID(TVDB_MovieID.Value) is { } tvdbMovie && !string.IsNullOrEmpty(tvdbMovie.Overview))
            overview = tvdbMovie.Overview;
        if (overview is null)
            return null;
        return new TextStub { Language = TitleLanguage.English, LanguageCode = "en", Value = overview, Source = DataSource.TvDB };
    }

    private IReadOnlyList<TMDB_Title> GetTmdbTitles()
    {
        // If we're linked to multiple TMDB shows or multiple TMDB movies, then
        // don't bother attempting to programmatically find the perfect title.
        if (TmdbShows is { Count: 1 } tmdbShows)
        {
            // TODO: Maybe add argumented season name support. Argumented in the sense that we add the season number or title to the show title per language.
            return tmdbShows[0].GetAllTitles();
        }
        else if (TmdbMovies is { Count: 1 } tmdbMovies)
        {
            return tmdbMovies[0].GetAllTitles();
        }

        return [];
    }

    private List<ITitle>? _animeTitles = null;

    public IReadOnlyList<ITitle> Titles => LoadAnimeTitles();

    public void ResetAnimeTitles()
    {
        _animeTitles = null;
        LoadAnimeTitles();
    }

    private List<ITitle> LoadAnimeTitles()
    {
        if (_animeTitles is not null)
            return _animeTitles;

        lock (this)
        {
            if (_animeTitles is not null)
                return _animeTitles;

            var titles = new List<ITitle>();
            var seriesOverrideTitle = false;
            if (!string.IsNullOrEmpty(SeriesNameOverride))
            {
                titles.Add(new TitleStub()
                {
                    Source = DataSource.User,
                    Language = TitleLanguage.Unknown,
                    LanguageCode = "unk",
                    Value = SeriesNameOverride,
                    Type = TitleType.Main,
                });
                seriesOverrideTitle = true;
            }

            var animeTitles = (this as IDaCollectorSeries).AnidbAnime.Titles.ToList();
            if (seriesOverrideTitle)
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
                        Type = TitleType.Official,
                    });
                }
            }
            titles.AddRange(animeTitles);
            titles.AddRange((this as IDaCollectorSeries).LinkedSeries.Where(e => e.Source is not DataSource.AniDB).SelectMany(ep => ep.Titles));

            return _animeTitles = titles;
        }
    }

    private bool _preferredOverviewLoaded = false;

    private IText? _preferredOverview = null;

    public IText? PreferredOverview => LoadPreferredOverview();

    public void ResetPreferredOverview()
    {
        _preferredOverviewLoaded = false;
        _preferredOverview = null;
        LoadPreferredOverview();
    }

    private IText? LoadPreferredOverview()
    {
        // Return the cached value if it's set.
        if (_preferredOverviewLoaded)
            return _preferredOverview;

        lock (this)
        {
            // Return the cached value if it's set.
            if (_preferredOverviewLoaded)
                return _preferredOverview;
            _preferredOverviewLoaded = true;

            var settings = Utils.SettingsProvider.GetSettings();
            var sourceOrder = settings.Language.DescriptionSourceOrder;
            var languageOrder = Languages.PreferredDescriptionNamingLanguages;
            var anidbOverview = AniDB_Anime?.Description;

            // Lazy load TMDB overviews if needed.
            IReadOnlyList<TMDB_Overview>? tmdbOverviews = null;

            // Check each language and source in the most preferred order.
            foreach (var language in languageOrder)
                foreach (var source in sourceOrder)
                {
                    IText? overview = source switch
                    {
                        DataSource.AniDB =>
                            language.Language is TitleLanguage.English && !string.IsNullOrEmpty(anidbOverview)
                                ? new TextStub
                                {
                                    Language = TitleLanguage.English,
                                    LanguageCode = "en",
                                    Value = anidbOverview,
                                    Source = DataSource.AniDB,
                                }
                                : null,
                        DataSource.TMDB =>
                            (tmdbOverviews ??= GetTmdbOverviews()).GetByLanguage(language.Language),
                        DataSource.TvDB =>
                            language.Language is TitleLanguage.English or TitleLanguage.EnglishAmerican or TitleLanguage.Main
                                ? GetTvdbOverview()
                                : null,
                        _ => null,
                    };
                    if (overview is not null)
                        return _preferredOverview = overview;
                }

            // Return nothing if no provider had an overview in the preferred language.
            return _preferredOverview = null;
        }

    }

    private IReadOnlyList<TMDB_Overview> GetTmdbOverviews()
    {
        // Get the overviews from TMDB if we're linked to a single show or
        // movie.
        if (TmdbShows is { Count: 1 } tmdbShows)
        {
            if (TmdbSeasonCrossReferences is not { Count: > 0 } tmdbSeasonCrossReferences)
                return [];

            // If we're linked to season zero, and only season zero, then we're
            // most likely linking an AniDB OVA to one or more specials of the
            // TMDB show, so do some additional logic on that.
            if (tmdbSeasonCrossReferences.Count is 1 && tmdbSeasonCrossReferences[0] is { SeasonNumber: 0 })
            {
                // If we're linking a single episode to a single episode, return
                // the overviews from the linked episode.
                var tmdbEpisodeCrossReferences = TmdbEpisodeCrossReferences
                    .Where(reference => reference.TmdbEpisodeID is not 0)
                    .ToList();
                if (tmdbEpisodeCrossReferences.Count is 1)
                    return tmdbEpisodeCrossReferences[0].TmdbEpisode?.GetAllOverviews() ?? [];

                // If we're linked to multiple episodes, then it will be hard to
                // construct an overview for each language without a lot of
                // additional logic, so just don't bother for now and return an
                // empty list.
                return [];
            }

            // Find the first season that's not season zero. If we found season
            // one, then return the overviews from the show, otherwise return
            // the overviews from the first found season.
            var tmdbSeasonCrossReference = tmdbSeasonCrossReferences
                .OrderBy(e => e.SeasonNumber)
                .First(tmdbSeason => tmdbSeason is not { SeasonNumber: 0 });
            if (tmdbSeasonCrossReference is { SeasonNumber: 1 })
                return tmdbShows[0].GetAllOverviews();

            return tmdbSeasonCrossReference.TmdbSeason?.GetAllOverviews() ?? [];
        }
        else if (TmdbMovies is { Count: 1 } tmdbMovies)
        {
            return tmdbMovies[0].GetAllOverviews();
        }

        return [];
    }

    #endregion

    public IReadOnlyList<CrossRef_File_Episode> FileCrossReferences => RepoFactory.CrossRef_File_Episode.GetByAnimeID(AniDB_ID);

    public IReadOnlyList<VideoLocal> VideoLocals => RepoFactory.VideoLocal.GetByAniDBAnimeID(AniDB_ID);

    public IReadOnlyList<AnimeSeason> AnimeSeasons => RepoFactory.AnimeEpisode.GetBySeriesID(AnimeSeriesID).Any(e => e.EpisodeType is EpisodeType.Special)
        ? [new AnimeSeason(this, EpisodeType.Episode, 1), new AnimeSeason(this, EpisodeType.Special, 0)]
        : [new AnimeSeason(this, EpisodeType.Episode, 1)];

    public IReadOnlyList<AnimeEpisode> AnimeEpisodes => RepoFactory.AnimeEpisode.GetBySeriesID(AnimeSeriesID)
        .Where(episode => !episode.IsHidden)
        .Select(episode => (episode, anidbEpisode: episode.AniDB_Episode))
        .OrderBy(tuple => tuple.anidbEpisode?.EpisodeType)
        .ThenBy(tuple => tuple.anidbEpisode?.EpisodeNumber)
        .Select(tuple => tuple.episode)
        .ToList();

    public IReadOnlyList<AnimeEpisode> AllAnimeEpisodes => RepoFactory.AnimeEpisode.GetBySeriesID(AnimeSeriesID)
        .Select(episode => (episode, anidbEpisode: episode.AniDB_Episode))
        .OrderBy(tuple => tuple.anidbEpisode?.EpisodeType)
        .ThenBy(tuple => tuple.anidbEpisode?.EpisodeNumber)
        .Select(tuple => tuple.episode)
        .ToList();

    #region Images

    public HashSet<ImageEntityType> GetAvailableImageTypes()
    {
        var images = new List<IImage>();
        var poster = AniDB_Anime?.GetImageMetadata(false);
        if (poster is not null)
            images.Add(poster);
        foreach (var xref in TmdbShowCrossReferences)
            images.AddRange(xref.GetImages());
        foreach (var xref in TmdbSeasonCrossReferences)
            images.AddRange(xref.GetImages());
        foreach (var xref in TmdbMovieCrossReferences.DistinctBy(xref => xref.TmdbMovieID))
            images.AddRange(xref.GetImages());
        return images
            .DistinctBy(image => image.ImageType)
            .Select(image => image.ImageType)
            .ToHashSet();
    }

    public HashSet<ImageEntityType> GetPreferredImageTypes()
        => RepoFactory.AniDB_Anime_PreferredImage.GetByAnimeID(AniDB_ID)
            .Select(preferredImage => preferredImage?.GetImageMetadata())
            .WhereNotNull()
            .Select(image => image.ImageType)
            .ToHashSet();

    public IImage? GetPreferredImageForType(ImageEntityType entityType)
        => RepoFactory.AniDB_Anime_PreferredImage.GetByAnidbAnimeIDAndType(AniDB_ID, entityType)?.GetImageMetadata();

    public IReadOnlyList<IImage> GetImages(ImageEntityType? entityType = null)
    {
        var preferredImages = (entityType.HasValue ? [RepoFactory.AniDB_Anime_PreferredImage.GetByAnidbAnimeIDAndType(AniDB_ID, entityType.Value)!] : RepoFactory.AniDB_Anime_PreferredImage.GetByAnimeID(AniDB_ID))
            .WhereNotNull()
            .Select(preferredImage => preferredImage.GetImageMetadata())
            .WhereNotNull()
            .DistinctBy(image => image.ImageType)
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
        foreach (var xref in TmdbShowCrossReferences)
            images.AddRange(xref.GetImages(entityType, preferredImages));
        foreach (var xref in TmdbSeasonCrossReferences)
            images.AddRange(xref.GetImages(entityType, preferredImages));
        foreach (var xref in TmdbMovieCrossReferences.DistinctBy(xref => xref.TmdbMovieID))
            images.AddRange(xref.GetImages(entityType, preferredImages));

        return images
            .DistinctBy(image => (image.ImageType, image.Source, image.ID))
            .ToList();
    }

    #endregion

    #region AniDB

    public AniDB_Anime? AniDB_Anime => RepoFactory.AniDB_Anime.GetByAnimeID(AniDB_ID);

    #endregion

    #region TMDB

    public IReadOnlyList<CrossRef_AniDB_TMDB_Movie> TmdbMovieCrossReferences => RepoFactory.CrossRef_AniDB_TMDB_Movie.GetByAnidbAnimeID(AniDB_ID);

    public IReadOnlyList<TMDB_Movie> TmdbMovies => TmdbMovieCrossReferences
        .DistinctBy(xref => xref.TmdbMovieID)
        .Select(xref => xref.TmdbMovie)
        .WhereNotNull()
        .ToList();

    public IReadOnlyList<CrossRef_AniDB_TMDB_Show> TmdbShowCrossReferences => RepoFactory.CrossRef_AniDB_TMDB_Show.GetByAnidbAnimeID(AniDB_ID);

    public IReadOnlyList<TMDB_Show> TmdbShows => TmdbShowCrossReferences
        .Select(xref => xref.TmdbShow)
        .WhereNotNull()
        .ToList();

    public IReadOnlyList<CrossRef_AniDB_TMDB_Episode> TmdbEpisodeCrossReferences => RepoFactory.CrossRef_AniDB_TMDB_Episode.GetByAnidbAnimeID(AniDB_ID);

    public IReadOnlyList<CrossRef_AniDB_TMDB_Episode> GetTmdbEpisodeCrossReferences(int? tmdbShowId = null) => tmdbShowId.HasValue
        ? RepoFactory.CrossRef_AniDB_TMDB_Episode.GetOnlyByAnidbAnimeAndTmdbShowIDs(AniDB_ID, tmdbShowId.Value)
        : RepoFactory.CrossRef_AniDB_TMDB_Episode.GetByAnidbAnimeID(AniDB_ID);

    public IReadOnlyList<CrossRef_AniDB_TMDB_Season> TmdbSeasonCrossReferences =>
        TmdbEpisodeCrossReferences
            .Select(xref => xref.TmdbSeasonCrossReference)
            .WhereNotNull()
            .DistinctBy(xref => xref.TmdbSeasonID)
            .ToList();

    public IReadOnlyList<TMDB_Season> TmdbSeasons => TmdbSeasonCrossReferences
        .Select(xref => xref.TmdbSeason)
        .WhereNotNull()
        .ToList();

    public IReadOnlyList<CrossRef_AniDB_TMDB_Season> GetTmdbSeasonCrossReferences(int? tmdbShowId = null) =>
        GetTmdbEpisodeCrossReferences(tmdbShowId)
            .Select(xref => xref.TmdbSeasonCrossReference)
            .WhereNotNull()
            .Distinct()
            .ToList();

    #endregion

    #region MAL

    public IReadOnlyList<CrossRef_AniDB_MAL> MalCrossReferences
        => RepoFactory.CrossRef_AniDB_MAL.GetByAnimeID(AniDB_ID);

    #endregion

    private DateTime? _airDate;

    public DateTime? AirDate
    {
        get
        {
            if (_airDate != null) return _airDate;
            var anime = AniDB_Anime;
            if (anime?.AirDate != null)
                return _airDate = anime.AirDate.Value;

            // This will be slower, but hopefully more accurate
            var ep = RepoFactory.AniDB_Episode.GetByAnimeID(AniDB_ID)
                .Where(a => a.EpisodeType is EpisodeType.Episode && a.LengthSeconds > 0 && a.AirDate != 0)
                .MinBy(a => a.AirDate);
            return _airDate = ep?.GetAirDateAsDate();
        }
    }

    private DateTime? _endDate;
    public DateTime? EndDate
    {
        get
        {
            if (_endDate != null) return _endDate;
            return _endDate = AniDB_Anime?.EndDate;
        }
    }

    public HashSet<int> Years
    {
        get
        {
            if (AniDB_Anime is not { } anime) return [];
            var startYear = anime.BeginYear;
            if (startYear == 0) return [];

            var endYear = anime.EndYear;
            if (endYear == 0) endYear = DateTime.Today.Year;
            if (endYear < startYear) endYear = startYear;
            if (startYear == endYear) return [startYear];

            return Enumerable.Range(startYear, endYear - startYear + 1).Where(anime.IsInYear).ToHashSet();
        }
    }

    /// <summary>
    /// Gets the direct parent AnimeGroup this series belongs to
    /// </summary>
    public AnimeGroup AnimeGroup => RepoFactory.AnimeGroup.GetByID(AnimeGroupID);

    /// <summary>
    /// Gets the very top level AnimeGroup which this series belongs to
    /// </summary>
    public AnimeGroup TopLevelAnimeGroup
    {
        get
        {
            var parentGroup = RepoFactory.AnimeGroup.GetByID(AnimeGroupID) ??
                throw new NullReferenceException($"Unable to find parent AnimeGroup {AnimeGroupID} for AnimeSeries {AnimeSeriesID}");

            int parentID;
            while ((parentID = parentGroup.AnimeGroupParentID ?? 0) != 0)
            {
                parentGroup = RepoFactory.AnimeGroup.GetByID(parentID) ??
                    throw new NullReferenceException($"Unable to find parent AnimeGroup {parentGroup.AnimeGroupParentID} for AnimeGroup {parentGroup.AnimeGroupID}");
            }

            return parentGroup;
        }
    }

    public List<AnimeGroup> AllGroupsAbove
    {
        get
        {
            var grps = new List<AnimeGroup>();
            var groupID = AnimeGroupID;
            while (groupID != 0)
            {
                var grp = RepoFactory.AnimeGroup.GetByID(groupID);
                if (grp != null)
                {
                    grps.Add(grp);
                    groupID = grp.AnimeGroupParentID ?? 0;
                }
                else
                {
                    groupID = 0;
                }
            }

            return grps;
        }
    }

    public override string ToString()
    {
        return $"Series: {AniDB_Anime?.MainTitle} ({AnimeSeriesID})";
        //return string.Empty;
    }

    #region IMetadata Implementation

    DataSource IMetadata.Source => DataSource.DaCollector;

    int IMetadata<int>.ID => AnimeSeriesID;

    #endregion

    #region IWithDescription Implementation

    IText? IWithDescriptions.DefaultDescription => AniDB_Anime is { Description.Length: > 0 } anime
        ? new TextStub()
        {
            Language = TitleLanguage.English,
            LanguageCode = "en",
            Value = anime.Description,
            Source = DataSource.AniDB,
        }
        : null;

    IText? IWithDescriptions.PreferredDescription => PreferredOverview;

    IReadOnlyList<IText> IWithDescriptions.Descriptions => (this as IDaCollectorSeries).LinkedSeries.SelectMany(ep => ep.Descriptions).ToList();

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
            if (AniDB_Anime is ISeries anidbAnime)
                list.AddRange(anidbAnime.Cast);
            foreach (var movie in TmdbMovies)
                list.AddRange(movie.Cast);
            foreach (var show in TmdbShows)
                list.AddRange(show.Cast);
            return list;
        }
    }

    IReadOnlyList<ICrew> IWithCastAndCrew.Crew
    {
        get
        {
            var list = new List<ICrew>();
            if (AniDB_Anime is ISeries anidbAnime)
                list.AddRange(anidbAnime.Crew);
            foreach (var movie in TmdbMovies)
                list.AddRange(movie.Crew);
            foreach (var show in TmdbShows)
                list.AddRange(show.Crew);
            return list;
        }
    }

    #endregion

    #region IWithStudios Implementation

    IReadOnlyList<IStudio> IWithStudios.Studios
    {
        get
        {
            var list = new List<IStudio>();
            if (AniDB_Anime is ISeries anidbAnime)
                list.AddRange(anidbAnime.Studios);
            foreach (var movie in TmdbMovies)
                list.AddRange(movie.TmdbStudios);
            foreach (var show in TmdbShows)
                list.AddRange(show.TmdbStudios);
            return list;
        }
    }

    #endregion

    #region IWithContentRatings Implementation

    IReadOnlyList<IContentRating> IWithContentRatings.ContentRatings
    {
        get
        {
            var list = new List<IContentRating>();
            if (AniDB_Anime is ISeries anidbAnime)
                list.AddRange(anidbAnime.ContentRatings);
            foreach (var movie in TmdbMovies)
                list.AddRange(movie.ContentRatings);
            foreach (var show in TmdbShows)
                list.AddRange(show.ContentRatings);
            return list;
        }
    }

    #endregion

    #region IWithYearlySeasons Implementation

    IReadOnlyList<(int Year, YearlySeason Season)> IWithYearlySeasons.YearlySeasons
        => [.. AirDate.GetYearlySeasons(EndDate)];

    #endregion

    #region ISeries Implementation

    AnimeType ISeries.Type => AniDB_Anime?.AnimeType ?? AnimeType.Unknown;

    IReadOnlyList<int> ISeries.DaCollectorSeriesIDs => [AnimeSeriesID];

    double ISeries.Rating => (AniDB_Anime?.Rating ?? 0) / 100D;

    int ISeries.RatingVotes => AniDB_Anime?.VoteCount ?? 0;

    bool ISeries.Restricted => AniDB_Anime?.IsRestricted ?? false;

    IReadOnlyList<IDaCollectorSeries> ISeries.DaCollectorSeries => [this];

    IImage? ISeries.DefaultPoster => AniDB_Anime?.GetImageMetadata();

    IReadOnlyList<IRelatedMetadata<ISeries, ISeries>> ISeries.RelatedSeries => [];

    IReadOnlyList<IRelatedMetadata<ISeries, IMovie>> ISeries.RelatedMovies => [];

    IReadOnlyList<IVideoCrossReference> ISeries.CrossReferences =>
        RepoFactory.CrossRef_File_Episode.GetByAnimeID(AniDB_ID);

    IReadOnlyList<ISeason> ISeries.Seasons => AnimeSeasons;

    IReadOnlyList<IEpisode> ISeries.Episodes => AllAnimeEpisodes;

    IReadOnlyList<IVideo> ISeries.Videos =>
        RepoFactory.CrossRef_File_Episode.GetByAnimeID(AniDB_ID)
            .DistinctBy(xref => xref.Hash)
            .Select(xref => xref.VideoLocal)
            .WhereNotNull()
            .ToList();

    EpisodeCounts ISeries.EpisodeCounts => (this as IDaCollectorSeries).AnidbAnime.EpisodeCounts;

    #endregion

    #region IDaCollectorSeries Implementation

    int IDaCollectorSeries.AnidbAnimeID => AniDB_ID;

    int IDaCollectorSeries.ParentGroupID => AnimeGroupID;

    int IDaCollectorSeries.TopLevelGroupID => TopLevelAnimeGroup.AnimeGroupID;

    IReadOnlyList<IDaCollectorTagForSeries> IDaCollectorSeries.Tags => RepoFactory.CustomTag.GetByAnimeID(AniDB_ID)
        .Select(x => new AnimeTag(x, this))
        .OrderBy(x => x.ID)
        .ToList();

    int IDaCollectorSeries.MissingEpisodeCount => MissingEpisodeCount;

    int IDaCollectorSeries.MissingCollectingEpisodeCount => MissingEpisodeCountGroups;

    int IDaCollectorSeries.HiddenMissingEpisodeCount => HiddenMissingEpisodeCount;

    int IDaCollectorSeries.HiddenMissingCollectingEpisodeCount => HiddenMissingEpisodeCountGroups;

    IDaCollectorGroup IDaCollectorSeries.ParentGroup => AnimeGroup;

    IDaCollectorGroup IDaCollectorSeries.TopLevelGroup => TopLevelAnimeGroup;

    IReadOnlyList<IDaCollectorGroup> IDaCollectorSeries.AllParentGroups => AllGroupsAbove;

    IAnidbAnime IDaCollectorSeries.AnidbAnime => AniDB_Anime ??
        throw new NullReferenceException($"Unable to find AniDB anime with id {AniDB_ID} in IDaCollectorSeries.AnidbAnime");

    bool IDaCollectorSeries.AnilistAutoMatchingDisabled => IsAniListAutoMatchingDisabled;

    IReadOnlyList<IAnilistAnime> IDaCollectorSeries.AnilistAnime => [];

    IReadOnlyList<IAnilistAnimeCrossReference> IDaCollectorSeries.AnilistAnimeCrossReferences => [];

    bool IDaCollectorSeries.TmdbAutoMatchingDisabled => IsTMDBAutoMatchingDisabled;

    IReadOnlyList<ITmdbShow> IDaCollectorSeries.TmdbShows => TmdbShows;

    IReadOnlyList<ITmdbSeason> IDaCollectorSeries.TmdbSeasons => TmdbSeasons;

    IReadOnlyList<ITmdbMovie> IDaCollectorSeries.TmdbMovies => TmdbMovies;

    IReadOnlyList<ITmdbShowCrossReference> IDaCollectorSeries.TmdbShowCrossReferences => TmdbShowCrossReferences;

    IReadOnlyList<ITmdbSeasonCrossReference> IDaCollectorSeries.TmdbSeasonCrossReferences => TmdbSeasonCrossReferences;

    IReadOnlyList<ITmdbEpisodeCrossReference> IDaCollectorSeries.TmdbEpisodeCrossReferences => TmdbEpisodeCrossReferences;

    IReadOnlyList<ITmdbMovieCrossReference> IDaCollectorSeries.TmdbMovieCrossReferences => TmdbMovieCrossReferences;

    IReadOnlyList<ISeries> IDaCollectorSeries.LinkedSeries
    {
        get
        {
            var seriesList = new List<ISeries>();

            var anidbAnime = AniDB_Anime;
            if (anidbAnime is not null)
                seriesList.Add(anidbAnime);

            seriesList.AddRange(TmdbShows);

            // Add more series here.

            return seriesList;
        }
    }

    IReadOnlyList<IMovie> IDaCollectorSeries.LinkedMovies
    {
        get
        {
            var movieList = new List<IMovie>();

            movieList.AddRange(TmdbMovies);

            // Add more movies here.

            return movieList;
        }
    }

    IReadOnlyList<IDaCollectorSeason> IDaCollectorSeries.Seasons => AnimeSeasons;

    IReadOnlyList<IDaCollectorEpisode> IDaCollectorSeries.Episodes => AllAnimeEpisodes;

    ISeriesUserData IDaCollectorSeries.GetUserData(IUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (user.ID is 0 || RepoFactory.JMMUser.GetByID(user.ID) is null)
            throw new ArgumentException("User is not stored in the database!", nameof(user));
        var userData = RepoFactory.AnimeSeries_User.GetByUserAndSeriesID(user.ID, AnimeSeriesID)
            ?? new() { JMMUserID = user.ID, AnimeSeriesID = AnimeSeriesID };
        if (userData.AnimeSeries_UserID is 0)
            RepoFactory.AnimeSeries_User.Save(userData);
        return userData;
    }

    #endregion
}
