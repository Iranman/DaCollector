using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
namespace DaCollector.Server.Filters;

[JsonConverter(typeof(StringEnumConverter))]
public enum FilterExpressionGroup
{
    Info,
    Logic,
    Function,
    Selector
}
