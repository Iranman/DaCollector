using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using DaCollector.Abstractions.Metadata;
using DaCollector.Abstractions.Metadata.Enums;

#nullable enable
namespace DaCollector.Server.API.v3.Models.MediaCatalog;

/// <summary>
/// API representation of a provider-specific media identity.
/// </summary>
public class MediaCatalogExternalId
{
    /// <summary>
    /// Metadata provider that owns the ID.
    /// </summary>
    [Required]
    [JsonConverter(typeof(StringEnumConverter))]
    public ExternalProvider Provider { get; init; }

    /// <summary>
    /// Kind of media identified by the provider ID.
    /// </summary>
    [Required]
    [JsonConverter(typeof(StringEnumConverter))]
    public MediaKind Kind { get; init; }

    /// <summary>
    /// Provider-specific ID value.
    /// </summary>
    [Required]
    public string Value { get; init; } = string.Empty;

    public MediaCatalogExternalId()
    {
    }

    public MediaCatalogExternalId(ExternalMediaId externalID)
    {
        Provider = externalID.Provider;
        Kind = externalID.Kind;
        Value = externalID.Value;
    }
}
