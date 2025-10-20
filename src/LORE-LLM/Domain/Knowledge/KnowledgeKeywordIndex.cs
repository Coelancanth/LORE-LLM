using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace LORE_LLM.Domain.Knowledge;

public sealed class KnowledgeKeywordIndex
{
    [JsonConstructor]
    public KnowledgeKeywordIndex(DateTimeOffset generatedAt, IReadOnlyList<KnowledgeKeywordIndexEntry> entries)
    {
        GeneratedAt = generatedAt;
        Entries = entries ?? Array.Empty<KnowledgeKeywordIndexEntry>();

        _keywordMap = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in Entries)
        {
            foreach (var keyword in entry.Keywords)
            {
                if (!_keywordMap.TryGetValue(keyword, out var titles))
                {
                    titles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    _keywordMap[keyword] = titles;
                }

                titles.Add(entry.Title);
            }
        }
    }

    public DateTimeOffset GeneratedAt { get; }

    public IReadOnlyList<KnowledgeKeywordIndexEntry> Entries { get; }

    private readonly Dictionary<string, HashSet<string>> _keywordMap;

    public IReadOnlyList<string> Lookup(string token)
    {
        var normalized = KnowledgeKeywordIndexEntry.NormalizeToken(token);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return Array.Empty<string>();
        }

        return _keywordMap.TryGetValue(normalized, out var titles)
            ? titles.ToList()
            : Array.Empty<string>();
    }
}

public sealed class KnowledgeKeywordIndexEntry
{
    [JsonConstructor]
    public KnowledgeKeywordIndexEntry(string title, IReadOnlyList<string> keywords)
    {
        Title = title;
        Keywords = keywords ?? Array.Empty<string>();
    }

    public string Title { get; }

    public IReadOnlyList<string> Keywords { get; }

    public static KnowledgeKeywordIndexEntry FromTitle(string title)
    {
        var tokens = TokenizeTitle(title);
        return new KnowledgeKeywordIndexEntry(title, tokens);
    }

    public static string NormalizeToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return string.Empty;
        }

        return new string(token
            .ToLowerInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray());
    }

    private static IReadOnlyList<string> TokenizeTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return Array.Empty<string>();
        }

        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var cleaned = title
            .Replace('_', ' ')
            .Replace('-', ' ')
            .Replace(':', ' ')
            .Replace('/', ' ');

        foreach (var part in cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var normalized = NormalizeToken(part);
            if (normalized.Length >= 3)
            {
                tokens.Add(normalized);
            }
        }

        var fullNormalized = NormalizeToken(title);
        if (fullNormalized.Length >= 3)
        {
            tokens.Add(fullNormalized);
        }

        return tokens.ToList();
    }
}
