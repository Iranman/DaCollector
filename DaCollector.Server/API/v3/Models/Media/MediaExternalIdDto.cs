using System.ComponentModel.DataAnnotations;

#nullable enable
namespace DaCollector.Server.API.v3.Models.Media;

public sealed class MediaExternalIdDto
{
    [Required]
    public string Source { get; init; } = string.Empty;

    [Required]
    public string Value { get; init; } = string.Empty;
}
