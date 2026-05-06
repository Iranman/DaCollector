using Newtonsoft.Json;
using DaCollector.Server.Plex.Models;
using DaCollector.Server.Plex.Models.Collection;
using DaCollector.Server.Plex.Models.Libraries;

using MediaContainer = DaCollector.Server.Plex.Models.Collection.MediaContainer;

namespace DaCollector.Server.Plex.Libraries;

internal class SVR_Directory : Directory
{
    public SVR_Directory(PlexHelper helper)
    {
        Helper = helper;
    }

    private PlexHelper Helper { get; }

    public PlexLibrary[] GetShows()
    {
        var (_, json) = Helper.RequestFromPlexAsync($"/library/sections/{Key}/all").ConfigureAwait(false)
            .GetAwaiter().GetResult();
        return JsonConvert
            .DeserializeObject<MediaContainer<MediaContainer>>(json, Helper.SerializerSettings)
            .Container.Metadata;
    }
}
