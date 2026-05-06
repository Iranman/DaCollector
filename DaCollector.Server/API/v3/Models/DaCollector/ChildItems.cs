using System.ComponentModel.DataAnnotations;

#nullable enable
namespace DaCollector.Server.API.v3.Models.DaCollector;

public class ChildItems
{
    [Required]
    public int Folders { get; set; }

    [Required]
    public int Files { get; set; }
}
