using System.Collections.Generic;
using DaCollector.Server.Providers.AniDB.HTTP.GetAnime;

namespace DaCollector.Server.Providers.AniDB.HTTP;

public class ResponseGetAnime
{
    public ResponseAnime Anime { get; set; }
    public List<ResponseTitle> Titles { get; set; }
    public List<ResponseEpisode> Episodes { get; set; }
    public List<ResponseTag> Tags { get; set; }
    public List<ResponseStaff> Staff { get; set; }
    public List<ResponseCharacter> Characters { get; set; }
    public List<ResponseResource> Resources { get; set; }
    public List<ResponseRelation> Relations { get; set; }
    public List<ResponseSimilar> Similar { get; set; }
}
