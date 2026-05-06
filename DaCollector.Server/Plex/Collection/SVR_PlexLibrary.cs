using Newtonsoft.Json;
using DaCollector.Server.Plex.Models;
using DaCollector.Server.Plex.Models.Collection;
using DaCollector.Server.Plex.Models.TVShow;
using MediaContainer = DaCollector.Server.Plex.Models.TVShow.MediaContainer;

namespace DaCollector.Server.Plex.Collection;

internal class SVR_PlexLibrary : PlexLibrary
{
    public SVR_PlexLibrary(PlexHelper helper)
    {
        Helper = helper;
    }

    private PlexHelper Helper { get; }

    public Episode[] GetEpisodes()
    {
        var (_, data) = Helper.RequestFromPlexAsync($"/library/metadata/{RatingKey}/allLeaves").GetAwaiter()
            .GetResult();
        return JsonConvert
            .DeserializeObject<MediaContainer<MediaContainer>>(data, Helper.SerializerSettings)
            .Container.Metadata;
    }
}
