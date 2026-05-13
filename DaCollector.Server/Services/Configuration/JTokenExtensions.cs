using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DaCollector.Server.Services.Configuration;

internal static class JTokenExtensions
{
    internal static string ToJson(this JToken token)
        => token.Type switch
        {
            JTokenType.Null or JTokenType.Undefined => "null",
            JTokenType.Boolean => token.Value<bool>().ToString().ToLowerInvariant(),
            JTokenType.String => JsonConvert.SerializeObject(token.Value<string>()),
            _ => token.ToString(),
        };
}
