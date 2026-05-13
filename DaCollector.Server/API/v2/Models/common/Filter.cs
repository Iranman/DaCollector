using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using DaCollector.Abstractions.Metadata.Enums;
using DaCollector.Abstractions.Extensions;
using DaCollector.Abstractions.Filtering.Services;
using DaCollector.Server.Models.DaCollector;
using DaCollector.Server.Repositories;

#nullable enable
namespace DaCollector.Server.API.v2.Models.common;

[DataContract]
public class Filter : Filters
{
    public override string type => string.Intern("filter");

    // We need to rethink this
    // There is too much duplicated info.
    // example:
    // groups { { name="the series" air="a date" year="2017" ... series { { name="the series" air="a date" year="2017" ... }, {...} } }
    // my plan is:
    // public List<BaseDirectory> subdirs;
    // structure:
    // subdirs { { type="group" name="the group" ... series {...} }, { type="serie" name="the series" ... eps {...} } }
    [DataMember(IsRequired = false, EmitDefaultValue = false)]
    public List<Group> groups { get; set; }

    public Filter()
    {
        art = new ArtCollection();
        groups = new List<Group>();
    }

    internal static Filter GenerateFromGroupFilter(HttpContext ctx, FilterPreset gf, int uid, bool noCast,
        bool noTag, int level,
        bool all, bool allPic, int pic, TagFilter.Filter tagFilter, IReadOnlyList<IGrouping<int, int>>? evaluatedResults = null)
    {
        var groups = new List<Group>();
        var filter = new Filter { name = gf.Name, id = gf.FilterPresetID, size = 0 };
        if (evaluatedResults == null)
        {
            var evaluator = ctx.RequestServices.GetRequiredService<IFilterEvaluator>();
            evaluatedResults = evaluator.EvaluateFilter(gf, ctx.GetUser()).ToList();
        }

        if (evaluatedResults.Count == 0)
        {
            filter.viewed = 0;
            filter.url = APIV2Helper.ConstructFilterIdUrl(ctx, filter.id);

            return filter;
        }

        filter.size = evaluatedResults.Count;

        // Populate Random Art
        List<MediaSeries>? arts = null;
        var seriesList = evaluatedResults
            .SelectMany(a => a)
            .Select(RepoFactory.MediaSeries.GetByID)
            .WhereNotNull()
            .ToList();
        var groupsList = evaluatedResults
            .Select(r => RepoFactory.MediaGroup.GetByID(r.Key))
            .Where(a => a is { MediaGroupParentID: null })
            .ToList();
        if (pic == 1)
        {
            arts = seriesList.Where(SeriesHasArt).ToList();

            if (arts.Count == 0) arts = seriesList;
        }

        if (arts?.Count > 0)
        {
            var rand = new Random();
            var anime = arts[rand.Next(arts.Count)];
            var backdrops = anime.GetImages(ImageEntityType.Backdrop);
            if (backdrops.Count > 0)
            {
                var backdrop = backdrops[rand.Next(backdrops.Count)];
                filter.art.fanart.Add(new Art
                {
                    index = 0,
                    url = APIHelper.ConstructImageLinkFromTypeAndId(ctx, backdrop.ImageType, backdrop.Source, backdrop.ID),
                });
            }

            filter.art.thumb.Add(new Art
            {
                index = 0,
                url = APIHelper.ConstructImageLinkFromTypeAndId(ctx, ImageEntityType.Poster, DataSource.AniDB, anime.AniDB_ID ?? 0),
            });
        }

        if (level > 0)
            groups.AddRange(groupsList.Select(ag => Group.GenerateFromAnimeGroup(ctx, ag, uid, noCast, noTag, level - 1, all, filter.id, allPic, pic, tagFilter,
                evaluatedResults?.FirstOrDefault(a => a.Key == ag.MediaGroupID)?.ToList())));

        if (groups.Count > 0) filter.groups = groups;

        filter.viewed = 0;
        filter.url = APIV2Helper.ConstructFilterIdUrl(ctx, filter.id);

        return filter;
    }

    private static bool SeriesHasArt(MediaSeries series)
    {
        return series.GetImages(ImageEntityType.Backdrop).Count is > 0;
    }
}
