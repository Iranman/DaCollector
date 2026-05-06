using DaCollector.Abstractions.Metadata.Enums;
using DaCollector.Abstractions.Extensions;

#nullable enable
namespace DaCollector.Server.API.v3.Models.Common;

public class LanguageDetails
{
    /// <summary>
    /// Language name.
    /// </summary>
    public string Name;

    /// <summary>
    /// Alpha 2 code, with `x-` extensions.
    /// </summary>
    public string Alpha2;

    public LanguageDetails(TitleLanguage language)
    {
        Name = language.GetDescription();
        Alpha2 = language.GetString();
    }
}
