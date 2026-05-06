using System.Runtime.Serialization;

namespace DaCollector.Server.Plex.Models.Login;

public class PlexAccount
{
    [DataMember(Name = "user")] public User User { get; set; }
}
