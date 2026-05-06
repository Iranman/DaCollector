using System.Runtime.Serialization;

namespace DaCollector.Server.Plex.Models.Login;

public class Roles
{
    [DataMember(Name = "roles")] public string[] PurpleRoles { get; set; }
}
