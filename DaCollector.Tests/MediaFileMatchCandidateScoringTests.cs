using System.Collections.Generic;
using System.Linq;
using DaCollector.Server.Media;
using Xunit;

namespace DaCollector.Tests;

public class MediaFileMatchCandidateScoringTests
{
    [Fact]
    public void ComputeScore_GivesPerfectScoreForExactTitleAndYear()
    {
        var reasons = new List<string>();

        var score = MediaFileMatchCandidateScoring.ComputeScore("The Matrix", 1999, "The Matrix", 1999, reasons);

        Assert.Equal(1.0, score);
        Assert.Contains("Exact title match", reasons);
        Assert.Contains("Year matches", reasons);
    }

    [Fact]
    public void ComputeScore_KeepsCloseTitleButBelowAutoPerfect()
    {
        var reasons = new List<string>();

        var score = MediaFileMatchCandidateScoring.ComputeScore("Gladiator", 2000, "Gladiator II", 2024, reasons);

        Assert.True(score >= 0.5);
        Assert.True(score < 1.0);
        Assert.Contains("Title substring match", reasons);
    }

    [Fact]
    public void PassesTitleFilter_RejectsUnrelatedTitles()
    {
        var queryNormalized = MediaFileMatchCandidateScoring.NormalizeTitle("The Matrix");
        var queryWords = new HashSet<string>(queryNormalized.Split(' '));

        Assert.False(MediaFileMatchCandidateScoring.PassesTitleFilter(queryWords, queryNormalized, "Breaking Bad"));
    }

    [Fact]
    public void ComputeScore_AddsRuntimeBonusWhenWithin5Min()
    {
        var reasons = new List<string>();

        var score = MediaFileMatchCandidateScoring.ComputeScore(
            "The Matrix", 1999, "The Matrix", 1999, reasons,
            queryRuntimeMinutes: 136, candidateRuntimeMinutes: 138);

        Assert.True(score >= 1.0, $"Expected >= 1.0 but got {score}");
        Assert.Contains("Runtime within 5 min", reasons);
    }

    [Fact]
    public void ComputeScore_AddsSmallRuntimeBonusWhenWithin10Min()
    {
        var reasons = new List<string>();

        var score = MediaFileMatchCandidateScoring.ComputeScore(
            "The Matrix", 1999, "The Matrix", 1999, reasons,
            queryRuntimeMinutes: 136, candidateRuntimeMinutes: 143);

        Assert.True(score >= 1.0, $"Expected >= 1.0 but got {score}");
        Assert.Contains("Runtime within 10 min", reasons);
        Assert.DoesNotContain("Runtime within 5 min", reasons);
    }

    [Fact]
    public void ComputeScore_NoRuntimeBonusWhenRuntimeFarOff()
    {
        var reasons = new List<string>();

        var score = MediaFileMatchCandidateScoring.ComputeScore(
            "The Matrix", 1999, "The Matrix", 1999, reasons,
            queryRuntimeMinutes: 136, candidateRuntimeMinutes: 90);

        Assert.Equal(1.0, score);
        Assert.DoesNotContain("Runtime within 5 min", reasons);
        Assert.DoesNotContain("Runtime within 10 min", reasons);
    }

    [Fact]
    public void ComputeScore_NoRuntimeBonusWhenCandidateRuntimeIsZero()
    {
        var reasons = new List<string>();

        var score = MediaFileMatchCandidateScoring.ComputeScore(
            "The Matrix", 1999, "The Matrix", 1999, reasons,
            queryRuntimeMinutes: 136, candidateRuntimeMinutes: 0);

        Assert.Equal(1.0, score);
        Assert.DoesNotContain(reasons, r => r.Contains("Runtime"));
    }
}
