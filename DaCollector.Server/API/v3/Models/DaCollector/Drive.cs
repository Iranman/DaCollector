using System.ComponentModel.DataAnnotations;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

#nullable enable
namespace DaCollector.Server.API.v3.Models.DaCollector;

public class Drive : Folder
{
    [Required, JsonConverter(typeof(StringEnumConverter))]
    public DriveType Type { get; set; }
}
