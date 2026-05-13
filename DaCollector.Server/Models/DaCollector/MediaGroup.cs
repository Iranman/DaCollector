using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using DaCollector.Abstractions.Metadata.Enums;
using DaCollector.Abstractions.Extensions;
using DaCollector.Abstractions.Metadata;
using DaCollector.Abstractions.Metadata.Containers;
using DaCollector.Abstractions.Metadata.DaCollector;
using DaCollector.Abstractions.Metadata.Stub;
using DaCollector.Server.Extensions;
using DaCollector.Server.Models.AniDB;
using DaCollector.Server.Repositories;

#nullable enable
namespace DaCollector.Server.Models.DaCollector;

public class MediaGroup : IDaCollectorGroup
{
    #region Server DB Columns

    public int MediaGroupID { get; set; }

    public int? MediaGroupParentID { get; set; }

    public string GroupName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int IsManuallyNamed { get; set; }

    public DateTime DateTimeUpdated { get; set; }

    public DateTime DateTimeCreated { get; set; }

    public DateTime? EpisodeAddedDate { get; set; }

    public DateTime? LatestEpisodeAirDate { get; set; }

    public int MissingEpisodeCount { get; set; }

    public int MissingEpisodeCountGroups { get; set; }

    public int OverrideDescription { get; set; }

    public int? DefaultMediaSeriesID { get; set; }

    public int? MainAniDBAnimeID { get; set; }

    #endregion

    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// Get a predictable sort name that stuffs everything that's not between
    /// A-Z under #.
    /// </summary>
    public string SortName
    {
        get
        {
            var sortName = !string.IsNullOrWhiteSpace(GroupName) ? GroupName.ToSortName().ToUpperInvariant() : "";
            var initialChar = (short)(sortName.Length > 0 ? sortName[0] : ' ');
            return initialChar is >= 65 and <= 90 ? sortName : "#" + sortName;
        }
    }

    public MediaGroup? Parent => MediaGroupParentID.HasValue ? RepoFactory.MediaGroup.GetByID(MediaGroupParentID.Value) : null;

    public List<MediaGroup> AllGroupsAbove
    {
        get
        {
            var allGroupsAbove = new List<MediaGroup>();
            var groupID = MediaGroupParentID;
            while (groupID.HasValue && groupID.Value != 0)
            {
                var grp = RepoFactory.MediaGroup.GetByID(groupID.Value);
                if (grp != null)
                {
                    allGroupsAbove.Add(grp);
                    groupID = grp.MediaGroupParentID;
                }
                else
                {
                    groupID = 0;
                }
            }

            return allGroupsAbove;
        }
    }

    public List<AniDB_Anime> Anime =>
        RepoFactory.MediaSeries.GetByGroupID(MediaGroupID).Select(s => s.AniDB_Anime).WhereNotNull().ToList();

    public decimal AniDBRating
    {
        get
        {
            try
            {
                decimal totalRating = 0;
                var totalVotes = 0;

                foreach (var anime in Anime)
                {
                    totalRating += anime.GetAniDBTotalRating();
                    totalVotes += anime.GetAniDBTotalVotes();
                }

                if (totalVotes == 0)
                {
                    return 0;
                }

                return totalRating / totalVotes;
            }
            catch (Exception ex)
            {
                _logger.Error($"Error in  AniDBRating: {ex}");
                return 0;
            }
        }
    }

    public List<MediaGroup> Children => RepoFactory.MediaGroup.GetByParentID(MediaGroupID);

    public IEnumerable<MediaGroup> AllChildren
    {
        get
        {
            var stack = new Stack<MediaGroup>();
            foreach (var child in Children)
            {
                stack.Push(child);
            }

            while (stack.Count > 0)
            {
                var current = stack.Pop();
                yield return current;
                foreach (var childGroup in current.Children) stack.Push(childGroup);
            }
        }
    }

    public MediaSeries? MainSeries
    {
        get
        {
            if (DefaultMediaSeriesID.HasValue)
            {
                var series = RepoFactory.MediaSeries.GetByID(DefaultMediaSeriesID.Value);
                if (series != null)
                    return series;
            }

            // Auto selected main series.
            if (MainAniDBAnimeID.HasValue)
            {
                var series = RepoFactory.MediaSeries.GetByAnimeID(MainAniDBAnimeID.Value);
                if (series != null)
                    return series;
            }

            return null;
        }
    }

    public List<MediaSeries> Series
    {
        get
        {
            var seriesList = RepoFactory.MediaSeries
                .GetByGroupID(MediaGroupID)
                .OrderBy(a => a.AirDate ?? DateTime.MaxValue)
                .ToList();

            // Make sure the default/main series is the first, if it's directly
            // within the group.
            if (!DefaultMediaSeriesID.HasValue && !MainAniDBAnimeID.HasValue) return seriesList;

            var mainSeries = MainSeries;
            if (mainSeries != null && seriesList.Remove(mainSeries)) seriesList.Insert(0, mainSeries);

            return seriesList;
        }
    }

    public List<MediaSeries> AllSeries
    {
        get
        {
            var seriesList = new List<MediaSeries>();
            var stack = new Stack<MediaGroup>();
            stack.Push(this);

            while (stack.Count > 0)
            {
                var current = stack.Pop();

                // get the series for this group
                var thisSeries = current.Series;
                seriesList.AddRange(thisSeries);

                foreach (var childGroup in current.Children)
                {
                    stack.Push(childGroup);
                }
            }

            seriesList = seriesList
                .OrderBy(a => a.AirDate ?? DateTime.MaxValue)
                .ToList();

            // Make sure the default/main series is the first if it's somewhere
            // within the group.
            if (DefaultMediaSeriesID.HasValue || MainAniDBAnimeID.HasValue)
            {
                MediaSeries? mainSeries = null;
                if (DefaultMediaSeriesID.HasValue)
                    mainSeries = seriesList.FirstOrDefault(ser => ser.MediaSeriesID == DefaultMediaSeriesID.Value);

                if (mainSeries == null && MainAniDBAnimeID.HasValue)
                    mainSeries = seriesList.FirstOrDefault(ser => ser.AniDB_ID == MainAniDBAnimeID.Value);

                if (mainSeries != null && seriesList.Remove(mainSeries)) seriesList.Insert(0, mainSeries);
            }

            return seriesList;
        }
    }


    public List<AniDB_Tag> Tags => AllSeries
        .SelectMany(ser => ser.AniDB_Anime?.AnimeTags ?? [])
        .OrderByDescending(a => a.Weight)
        .Select(animeTag => RepoFactory.AniDB_Tag.GetByTagID(animeTag.TagID))
        .WhereNotNull()
        .DistinctBy(a => a.TagID)
        .ToList();

    public List<CustomTag> CustomTags => AllSeries
        .SelectMany(ser => RepoFactory.CustomTag.GetByAnimeID(ser.AniDB_ID ?? 0))
        .DistinctBy(a => a.CustomTagID)
        .OrderBy(a => a.TagName)
        .ToList();

    public HashSet<int> Years => AllSeries.SelectMany(a => a.Years).ToHashSet();

    public HashSet<(int Year, YearlySeason Season)> YearlySeasons => AllSeries.SelectMany(a => a.AniDB_Anime?.YearlySeasons ?? []).ToHashSet();

    public HashSet<ImageEntityType> AvailableImageTypes => AllSeries
        .SelectMany(ser => ser.GetAvailableImageTypes())
        .ToHashSet();

    public HashSet<ImageEntityType> PreferredImageTypes => AllSeries
        .SelectMany(ser => ser.GetPreferredImageTypes())
        .ToHashSet();

    public List<AniDB_Anime_Title> Titles => AllSeries
        .SelectMany(ser => ser.AniDB_Anime?.Titles ?? [])
        .DistinctBy(tit => tit.AniDB_Anime_TitleID)
        .ToList();

    public override string ToString()
        => $"Group: {GroupName} ({MediaGroupID})";

    public MediaGroup TopLevelMediaGroup
    {
        get
        {
            var parent = Parent;
            if (parent == null)
            {
                return this;
            }

            while (true)
            {
                var next = parent.Parent;
                if (next == null)
                {
                    return parent;
                }

                parent = next;
            }
        }
    }

    public bool IsDescendantOf(int groupID)
        => IsDescendantOf(new int[] { groupID });

    public bool IsDescendantOf(IEnumerable<int> groupIDs)
    {
        var idSet = groupIDs.ToHashSet();
        if (idSet.Count == 0)
            return false;

        var parent = Parent;
        while (parent != null)
        {
            if (idSet.Contains(parent.MediaGroupID))
                return true;

            parent = parent.Parent;
        }

        return false;
    }

    #region IMetadata Implementation

    DataSource IMetadata.Source => DataSource.DaCollector;

    string IMetadata<string>.ID => MediaGroupID.ToString();

    int IMetadata<int>.ID => MediaGroupID;

    #endregion

    #region IWithTitles Implementation

    string IWithTitles.Title => IsManuallyNamed == 1
        ? GroupName
        : (this as IDaCollectorGroup).MainSeries.Title;

    ITitle IWithTitles.DefaultTitle => IsManuallyNamed == 1
        ? new TitleStub
        {
            Language = TitleLanguage.Unknown,
            LanguageCode = "unk",
            Type = TitleType.Main,
            Value = GroupName,
            Source = DataSource.User,
        }
        : (this as IDaCollectorGroup).MainSeries.DefaultTitle;

    ITitle? IWithTitles.PreferredTitle => IsManuallyNamed == 1
        ? new TitleStub
        {
            Language = TitleLanguage.Unknown,
            LanguageCode = "unk",
            Type = TitleType.Main,
            Value = GroupName,
            Source = DataSource.User,
        }
        : (this as IDaCollectorGroup).MainSeries.PreferredTitle;

    IReadOnlyList<ITitle> IWithTitles.Titles
    {
        get
        {
            var titles = new List<ITitle>();
            if (IsManuallyNamed == 1)
                titles.Add(new TitleStub
                {
                    Language = TitleLanguage.Unknown,
                    LanguageCode = "unk",
                    Value = GroupName,
                    Source = DataSource.User,
                    Type = TitleType.Main,
                });

            var mainSeriesId = (this as IDaCollectorGroup).MainSeriesID;
            foreach (var series in (this as IDaCollectorGroup).AllSeries)
            {
                foreach (var title in series.Titles)
                {
                    if ((IsManuallyNamed == 1 || series.ID != mainSeriesId) && title.Type == TitleType.Main)
                    {
                        titles.Add(new TitleStub()
                        {
                            Language = title.Language,
                            LanguageCode = title.LanguageCode,
                            CountryCode = title.CountryCode,
                            Value = title.Value,
                            Source = title.Source,
                            Type = TitleType.Official,
                        });
                        continue;
                    }
                    titles.Add(title);
                }
            }

            return titles;
        }
    }

    #endregion

    #region IWithDescription Implementation

    IText? IWithDescriptions.DefaultDescription => OverrideDescription == 1
        ? new TextStub()
        {
            Source = DataSource.DaCollector,
            Language = TitleLanguage.Unknown,
            LanguageCode = "unk",
            Value = Description,
        }
        : (this as IDaCollectorGroup).MainSeries.DefaultDescription;

    IText? IWithDescriptions.PreferredDescription => OverrideDescription == 1
        ? new TextStub()
        {
            Source = DataSource.DaCollector,
            Language = TitleLanguage.Unknown,
            LanguageCode = "unk",
            Value = Description,
        }
        : (this as IDaCollectorGroup).MainSeries.PreferredDescription;

    IReadOnlyList<IText> IWithDescriptions.Descriptions
    {
        get
        {
            var titles = new List<IText>();
            if (OverrideDescription == 1)
            {
                titles.Add(new TextStub()
                {
                    Source = DataSource.DaCollector,
                    Language = TitleLanguage.Unknown,
                    LanguageCode = "unk",
                    Value = Description,
                });
            }

            foreach (var series in (this as IDaCollectorGroup).AllSeries)
                titles.AddRange(series.Descriptions);

            return titles;
        }
    }

    #endregion

    #region IWithCreationDate Implementation

    DateTime IWithCreationDate.CreatedAt => DateTimeCreated.ToUniversalTime();

    #endregion

    #region IWithUpdateDate Implementation

    DateTime IWithUpdateDate.LastUpdatedAt => DateTimeUpdated.ToUniversalTime();

    #endregion

    #region IDaCollectorGroup Implementation

    int IDaCollectorGroup.ID => MediaGroupID;

    int? IDaCollectorGroup.ParentGroupID => MediaGroupParentID;

    int IDaCollectorGroup.TopLevelGroupID => TopLevelMediaGroup.MediaGroupID;

    int IDaCollectorGroup.MainSeriesID => (this as IDaCollectorGroup).MainSeries.ID;

    bool IDaCollectorGroup.HasConfiguredMainSeries => DefaultMediaSeriesID.HasValue;

    bool IDaCollectorGroup.HasCustomTitle => IsManuallyNamed == 1;

    bool IDaCollectorGroup.HasCustomDescription => OverrideDescription == 1;

    IDaCollectorGroup? IDaCollectorGroup.ParentGroup => Parent;

    IDaCollectorGroup IDaCollectorGroup.TopLevelGroup => TopLevelMediaGroup;

    IReadOnlyList<IDaCollectorGroup> IDaCollectorGroup.Groups => Children;

    IReadOnlyList<IDaCollectorGroup> IDaCollectorGroup.AllGroups => AllChildren.ToList();

    IDaCollectorSeries IDaCollectorGroup.MainSeries => MainSeries ?? AllSeries.FirstOrDefault() ??
        throw new NullReferenceException($"Unable to get main series for group {MediaGroupID} when accessed through IDaCollectorGroup.MainSeries");

    IReadOnlyList<IDaCollectorSeries> IDaCollectorGroup.Series => Series;

    IReadOnlyList<IDaCollectorSeries> IDaCollectorGroup.AllSeries => AllSeries;

    #endregion
}
