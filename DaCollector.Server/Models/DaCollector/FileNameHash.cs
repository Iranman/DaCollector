using System;

#nullable enable
namespace DaCollector.Server.Models.DaCollector;

public class FileNameHash
{
    public int FileNameHashID { get; set; }

    public string FileName { get; set; } = string.Empty;

    public long FileSize { get; set; }

    public string Hash { get; set; } = string.Empty;

    public DateTime DateTimeUpdated { get; set; }
}
