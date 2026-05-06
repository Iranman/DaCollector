using DaCollector.Abstractions.Metadata.Enums;

namespace DaCollector.Server.Providers.AniDB.HTTP.GetAnime;

public class ResponseTitle
{
    public TitleType TitleType { get; set; }
    public TitleLanguage Language { get; set; }
    public string Title { get; set; }
}
