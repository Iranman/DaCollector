using System.Runtime.Serialization;

namespace DaCollector.Server.Plex.Models;

public class TagHolder
{
    [DataMember(Name = "tag")] public string Tag { get; set; }
}
