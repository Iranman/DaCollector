
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using DaCollector.Server.Server;

#nullable enable
namespace DaCollector.Server.API.v3.Models.DaCollector;

public class IntegrityCheck
{
    [Required]
    public int ID { get; set; }

    [Required]
    public List<int> ManagedFolderIDs { get; set; } = [];

    [Required, JsonConverter(typeof(StringEnumConverter))]
    public ScanStatus Status { get; set; }

    [Required]
    public DateTime CreatedAt { get; set; }

    public int TotalFiles { get; set; }
    public int WaitingFiles { get; set; }
    public int ErrorFiles { get; set; }
    public int CompletedFiles { get; set; }
}

public record IntegrityCheckFile
{
    public int ID { get; init; }
    public int ScanID { get; init; }
    public int ManagedFolderID { get; init; }
    public int VideoLocalPlaceID { get; init; }
    public string FullName { get; init; } = string.Empty;
    public long FileSize { get; init; }
    [JsonConverter(typeof(StringEnumConverter))]
    public ScanFileStatus Status { get; init; }
    public DateTime? CheckDate { get; init; }
    public string? Hash { get; init; }
    public string? HashResult { get; init; }
}
