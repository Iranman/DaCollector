using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using DaCollector.Server.API.v3.Models.Media;

#nullable enable
namespace DaCollector.Server.API.v3.Models.Collections;

/// <summary>
/// One member of a managed collection, with its matched local file locations.
/// </summary>
public sealed record CollectionMemberDto
{
    [Required]
    public string Provider { get; init; } = string.Empty;

    [Required]
    public string ProviderID { get; init; } = string.Empty;

    [Required]
    public string Kind { get; init; } = string.Empty;

    [Required]
    public string Title { get; init; } = string.Empty;

    public string? Summary { get; init; }

    [Required]
    public IReadOnlyList<MediaFileLocationDto> Files { get; init; } = [];
}
