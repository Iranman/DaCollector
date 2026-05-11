using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

#nullable enable
namespace DaCollector.Server.Media;

public static partial class MediaFileMatchCandidateScoring
{
    private const double MinComparableTitleScore = 0.5;

    [GeneratedRegex(@"[^a-z0-9 ]", RegexOptions.Compiled)]
    private static partial Regex NonTitleCharacterRegex();

    public static bool PassesTitleFilter(HashSet<string> queryWords, string queryNormalized, string? candidateTitle)
    {
        if (string.IsNullOrWhiteSpace(candidateTitle))
            return false;
        var normalized = NormalizeTitle(candidateTitle);
        if (normalized.Length == 0)
            return false;
        if (normalized.Contains(queryNormalized, StringComparison.Ordinal) || queryNormalized.Contains(normalized, StringComparison.Ordinal))
            return true;
        foreach (var word in normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            if (queryWords.Contains(word))
                return true;
        return false;
    }

    public static double ComputeScore(
        string queryTitle,
        int? queryYear,
        string candidateTitle,
        int? candidateYear,
        List<string> reasons,
        int? queryRuntimeMinutes = null,
        int? candidateRuntimeMinutes = null)
    {
        if (string.IsNullOrWhiteSpace(candidateTitle))
            return 0;

        var query = NormalizeTitle(queryTitle);
        var candidate = NormalizeTitle(candidateTitle);

        if (query.Length == 0 || candidate.Length == 0)
            return 0;

        double titleScore;
        if (query == candidate)
        {
            titleScore = 1.0;
            reasons.Add("Exact title match");
        }
        else if (candidate.Contains(query, StringComparison.Ordinal) || query.Contains(candidate, StringComparison.Ordinal))
        {
            titleScore = 0.85;
            reasons.Add("Title substring match");
        }
        else
        {
            var queryWords = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var candidateWordSet = candidate.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .ToHashSet(StringComparer.Ordinal);
            var matchedWords = queryWords.Count(candidateWordSet.Contains);
            titleScore = queryWords.Length > 0 ? (double)matchedWords / queryWords.Length : 0;
            if (matchedWords > 0)
                reasons.Add($"{matchedWords}/{queryWords.Length} title words matched");
        }

        if (titleScore < MinComparableTitleScore)
            return titleScore;

        var yearBonus = 0.0;
        if (queryYear.HasValue && candidateYear.HasValue)
        {
            if (queryYear == candidateYear)
            {
                yearBonus = 0.1;
                reasons.Add("Year matches");
            }
            else if (Math.Abs(queryYear.Value - candidateYear.Value) <= 1)
            {
                yearBonus = 0.05;
                reasons.Add("Year within 1");
            }
        }

        var runtimeBonus = 0.0;
        if (queryRuntimeMinutes.HasValue && candidateRuntimeMinutes is > 0)
        {
            var diff = Math.Abs(queryRuntimeMinutes.Value - candidateRuntimeMinutes.Value);
            if (diff <= 5)
            {
                runtimeBonus = 0.08;
                reasons.Add("Runtime within 5 min");
            }
            else if (diff <= 10)
            {
                runtimeBonus = 0.04;
                reasons.Add("Runtime within 10 min");
            }
        }

        return Math.Min(1.0, titleScore + yearBonus + runtimeBonus);
    }

    public static string NormalizeTitle(string title)
        => string.Join(' ', NonTitleCharacterRegex().Replace(title.ToLowerInvariant().Trim(), " ")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
}
