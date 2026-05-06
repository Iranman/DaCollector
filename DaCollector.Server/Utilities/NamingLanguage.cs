using DaCollector.Abstractions.Metadata.Enums;
using DaCollector.Abstractions.Extensions;

namespace DaCollector.Server.Utilities;

public class NamingLanguage
{
    public TitleLanguage Language { get; set; }

    public string LanguageCode => Language.GetString();

    public string LanguageDescription => Language.GetDescription();

    public NamingLanguage(TitleLanguage language)
    {
        Language = language;
    }

    public NamingLanguage(string language)
    {
        Language = language.GetTitleLanguage();
    }

    public override string ToString()
    {
        return string.Format("{0} - ({1})", Language, LanguageDescription);
    }
}
