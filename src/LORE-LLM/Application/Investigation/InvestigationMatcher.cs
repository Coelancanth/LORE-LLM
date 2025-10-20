using System;
using System.Collections.Generic;
using System.Linq;
using LORE_LLM.Domain.Investigation;
using LORE_LLM.Domain.Knowledge;

namespace LORE_LLM.Application.Investigation;

internal sealed class InvestigationMatcher
{
    public IReadOnlyList<InvestigationMatch> Match(
        string segmentText,
        IReadOnlyCollection<string> tokens,
        IReadOnlyList<KnowledgeEntry> knowledgeEntries)
    {
        if (knowledgeEntries.Count == 0 || tokens.Count == 0)
        {
            return Array.Empty<InvestigationMatch>();
        }

        var matches = new List<InvestigationMatch>();
        var segmentNormalized = segmentText ?? string.Empty;

        foreach (var entry in knowledgeEntries)
        {
            var labels = BuildLabels(entry);
            if (labels.Count == 0)
            {
                continue;
            }

            var contributingTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var bestScore = 0.0;
            string? bestToken = null;
            string? note = null;

            foreach (var token in tokens)
            {
                foreach (var label in labels)
                {
                    var (score, reason) = ScoreToken(segmentNormalized, token, label);
                    if (score <= 0)
                    {
                        continue;
                    }

                    if (score >= 0.6)
                    {
                        contributingTokens.Add(token);
                    }

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestToken = token;
                        note = reason;
                    }
                }
            }

            if (bestScore < 0.55 || bestToken is null)
            {
                continue;
            }

            if (contributingTokens.Count == 0)
            {
                contributingTokens.Add(bestToken);
            }

            matches.Add(new InvestigationMatch(
                new InvestigationCandidate(
                    entry.ConceptId,
                    entry.Source.Url,
                    Math.Round(bestScore, 2, MidpointRounding.AwayFromZero),
                    note),
                contributingTokens.ToList()));
        }

        return matches
            .GroupBy(match => match.Candidate.ConceptId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(m => m.Candidate.Confidence).First())
            .OrderByDescending(match => match.Candidate.Confidence)
            .Take(5)
            .ToList();
    }

    private static IReadOnlyList<string> BuildLabels(KnowledgeEntry entry)
    {
        var labels = new List<string> { entry.Title };

        if (entry.Aliases is not null)
        {
            labels.AddRange(entry.Aliases);
        }

        return labels
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Select(label => label.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static (double Score, string Reason) ScoreToken(string segmentText, string token, string label)
    {
        if (string.Equals(token, label, StringComparison.OrdinalIgnoreCase))
        {
            return (0.95, $"Exact token match for \"{label}\"");
        }

        if (segmentText.Contains(label, StringComparison.OrdinalIgnoreCase))
        {
            return (0.9, $"Segment mentions \"{label}\"");
        }

        if (label.StartsWith(token, StringComparison.OrdinalIgnoreCase))
        {
            return (0.8, $"Token matches the start of \"{label}\"");
        }

        if (label.Contains(token, StringComparison.OrdinalIgnoreCase))
        {
            return (0.7, $"Token appears within \"{label}\"");
        }

        if (segmentText.Contains(token, StringComparison.OrdinalIgnoreCase))
        {
            return (0.6, $"Segment includes token \"{token}\"");
        }

        return (0.0, string.Empty);
    }
}

internal sealed record InvestigationMatch(
    InvestigationCandidate Candidate,
    IReadOnlyList<string> Tokens);
