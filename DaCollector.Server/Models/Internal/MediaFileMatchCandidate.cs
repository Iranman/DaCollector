using System;
using System.Collections.Generic;
using System.Text.Json;

#nullable enable
namespace DaCollector.Server.Models.Internal;

/// <summary>
/// A candidate provider match for an unmatched local media file, pending user review.
/// </summary>
public class MediaFileMatchCandidate
{
    public int MediaFileMatchCandidateID { get; set; }

    public int VideoLocalID { get; set; }

    /// <summary>Provider name: "tmdb" or "tvdb".</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>Provider-specific integer ID for the matched item.</summary>
    public int ProviderItemID { get; set; }

    /// <summary>Media type: "show" or "movie".</summary>
    public string ProviderType { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public int? Year { get; set; }

    /// <summary>Confidence score in [0, 1] derived from parser hints and title/year similarity.</summary>
    public double ConfidenceScore { get; set; }

    /// <summary>JSON-encoded list of human-readable reasons that contributed to the score.</summary>
    public string ReasonsJson { get; set; } = "[]";

    /// <summary>Candidate status: "Pending", "Approved", or "Rejected".</summary>
    public string Status { get; set; } = "Pending";

    public DateTime? ReviewedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public IReadOnlyList<string> Reasons
    {
        get
        {
            try { return JsonSerializer.Deserialize<List<string>>(ReasonsJson) ?? []; }
            catch { return []; }
        }
    }
}
