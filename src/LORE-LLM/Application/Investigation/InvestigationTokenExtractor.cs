using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace LORE_LLM.Application.Investigation;

internal static class InvestigationTokenExtractor
{
    private static readonly Regex TokenRegex = new(@"\b[A-Z][\w'-]{1,}\b", RegexOptions.Compiled);

    private static readonly HashSet<string> Stopwords = new(StringComparer.OrdinalIgnoreCase)
    {
        "The", "And", "But", "For", "With", "Your", "You", "I'm", "I'll", "I'd", "I've",
        "We", "Our", "They", "Their", "Them", "This", "That", "These", "Those", "It's",
        "He", "She", "His", "Her", "Its", "Is", "Are", "Was", "Were", "Be", "Do", "Did",
        "Doctor", "Death", "Yes", "No", "Why", "How", "What", "When", "Where", "Who",
        "Time", "Now", "Please", "Go", "Well", "Farewell", "Saturday"
    };

    public static IReadOnlyList<string> ExtractTokens(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<string>();
        }

        var tokens = new List<string>();
        foreach (Match match in TokenRegex.Matches(text))
        {
            var token = match.Value.Trim('\'');
            if (token.Length <= 1)
            {
                continue;
            }

            if (Stopwords.Contains(token))
            {
                continue;
            }

            tokens.Add(token);
        }

        return tokens
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
