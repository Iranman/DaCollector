using System;
using System.Collections.Generic;
using System.Linq;
using DaCollector.Abstractions.Metadata.Enums;
using DaCollector.Abstractions.Metadata;
using DaCollector.Abstractions.Metadata.Containers;
using DaCollector.Abstractions.Metadata.DaCollector;
using DaCollector.Abstractions.Metadata.Stub;

#nullable enable
namespace DaCollector.Server.Models.DaCollector.Embedded;

public class AnimeSeason(IDaCollectorSeries series, EpisodeType episodeType, int seasonNumber) : IDaCollectorSeason
{
    int ISeason.SeriesID => series.ID;

    int ISeason.SeasonNumber => seasonNumber;

    IImage? ISeason.DefaultPoster
        => seasonNumber is 0 ? null : series.DefaultPoster;

    ISeries ISeason.Series => series;

    IReadOnlyList<IEpisode> ISeason.Episodes => series.Episodes
        .Where(x => x.Type == episodeType && x.SeasonNumber == seasonNumber)
        .ToList();

    string IWithTitles.Title
        => seasonNumber is 0
        ? "Specials"
        : series.Title;

    ITitle IWithTitles.DefaultTitle
        => seasonNumber is 0
            ? new TitleStub()
            {
                Language = TitleLanguage.English,
                LanguageCode = "en",
                Value = "Specials",
                Source = DataSource.DaCollector,
                Type = TitleType.Official,
            }
            : series.DefaultTitle;

    ITitle? IWithTitles.PreferredTitle
        => seasonNumber is 0
            ? new TitleStub()
            {
                Language = TitleLanguage.English,
                LanguageCode = "en",
                Value = "Specials",
                Source = DataSource.DaCollector,
                Type = TitleType.Official,
            }
            : series.PreferredTitle;

    IReadOnlyList<ITitle> IWithTitles.Titles => seasonNumber is 0
        ? [
            new TitleStub()
            {
                Language = TitleLanguage.English,
                LanguageCode = "en",
                Value = "Specials",
                Source = DataSource.DaCollector,
                Type = TitleType.Official,
            },
        ]
        : series.Titles;

    IText? IWithDescriptions.DefaultDescription
        => seasonNumber is 0
            ? new TextStub()
            {
                Language = TitleLanguage.English,
                LanguageCode = "en",
                Value = "Specials",
                Source = DataSource.DaCollector,
            }
            : series.DefaultDescription;

    IText? IWithDescriptions.PreferredDescription
        => seasonNumber is 0
            ? new TextStub()
            {
                Language = TitleLanguage.English,
                LanguageCode = "en",
                Value = "Specials",
                Source = DataSource.DaCollector,
            }
            : series.PreferredDescription;

    IReadOnlyList<IText> IWithDescriptions.Descriptions => seasonNumber is 0
        ? [
            new TextStub()
            {
                Language = TitleLanguage.English,
                LanguageCode = "en",
                Value = "Specials",
                Source = DataSource.DaCollector,
            },
        ]
        : series.Descriptions;

    DateTime IWithCreationDate.CreatedAt => series.CreatedAt;

    DateTime IWithUpdateDate.LastUpdatedAt => series.LastUpdatedAt;

    IReadOnlyList<ICast> IWithCastAndCrew.Cast => series.Cast;

    IReadOnlyList<ICrew> IWithCastAndCrew.Crew => series.Crew;

    string IMetadata<string>.ID => series.ID.ToString();

    DataSource IMetadata.Source => DataSource.AniDB;

    IDaCollectorSeries IDaCollectorSeason.Series => series;

    IReadOnlyList<IDaCollectorEpisode> IDaCollectorSeason.Episodes => series.Episodes
        .Where(x => x.Type == episodeType && x.SeasonNumber == seasonNumber)
        .ToList();

    IReadOnlyList<(int Year, YearlySeason Season)> IWithYearlySeasons.YearlySeasons
        => seasonNumber is 0 ? [] : series.YearlySeasons;

    IImage? IWithImages.GetPreferredImageForType(ImageEntityType entityType)
        => seasonNumber is 0 ? null : series.GetPreferredImageForType(entityType);

    IReadOnlyList<IImage> IWithImages.GetImages(ImageEntityType? entityType)
        => seasonNumber is 0 ? [] : series.GetImages(entityType);
}
