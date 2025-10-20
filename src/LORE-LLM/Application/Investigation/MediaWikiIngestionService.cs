using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using LORE_LLM.Domain.Knowledge;

namespace LORE_LLM.Application.Investigation;

public sealed class MediaWikiIngestionService : IMediaWikiIngestionService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private static readonly Regex HtmlTagRegex = new("<.*?>", RegexOptions.Compiled | RegexOptions.Singleline);

    private readonly HttpClient _httpClient;

    public MediaWikiIngestionService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<Result<KnowledgeBaseDocument>> EnsureKnowledgeBaseAsync(
        DirectoryInfo projectDirectory,
        string sanitizedProject,
        string projectDisplayName,
        IEnumerable<string> candidateTokens,
        bool forceRefresh,
        CancellationToken cancellationToken)
    {
        var knowledgePath = Path.Combine(projectDirectory.FullName, "knowledge_base.json");
        if (!forceRefresh && File.Exists(knowledgePath))
        {
            var cachedDocument = await DeserializeKnowledgeAsync(knowledgePath, cancellationToken);
            if (cachedDocument is not null)
            {
                return Result.Success(cachedDocument);
            }
        }

        var apiBase = ResolveApiBase(sanitizedProject);
        if (apiBase is null)
        {
            return Result.Failure<KnowledgeBaseDocument>($"No MediaWiki configuration available for project '{sanitizedProject}'.");
        }

        var tokens = candidateTokens
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .Select(token => token.Trim())
            .Where(token => token.Length >= 3)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(50)
            .ToList();

        var cache = await LoadCacheAsync(projectDirectory, cancellationToken);
        var cacheUpdated = false;
        var entries = new List<KnowledgeEntry>();

        foreach (var token in tokens)
        {
            if (cache.TryGetValue(token, out var cachedEntry))
            {
                entries.Add(cachedEntry);
                continue;
            }

            var entry = await FetchEntryForTokenAsync(apiBase, token, cancellationToken);
            if (entry is null)
            {
                continue;
            }

            entries.Add(entry);
            cache[token] = entry;
            cacheUpdated = true;
        }

        if (entries.Count == 0)
        {
            return Result.Failure<KnowledgeBaseDocument>("No wiki knowledge entries could be retrieved for the provided tokens.");
        }

        var orderedEntries = entries
            .GroupBy(entry => entry.ConceptId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderBy(e => e.Title, StringComparer.OrdinalIgnoreCase).First())
            .OrderBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var document = new KnowledgeBaseDocument(
            sanitizedProject,
            projectDisplayName,
            DateTimeOffset.UtcNow,
            orderedEntries);

        await using (var stream = File.Create(knowledgePath))
        {
            await JsonSerializer.SerializeAsync(stream, document, JsonOptions, cancellationToken);
        }

        if (cacheUpdated)
        {
            await SaveCacheAsync(projectDirectory, cache, cancellationToken);
        }

        return Result.Success(document);
    }

    private static string? ResolveApiBase(string sanitizedProject) => sanitizedProject switch
    {
        "pathologic2-marble-nest" => "https://pathologic.fandom.com/api.php",
        _ => null
    };

    private async Task<KnowledgeBaseDocument?> DeserializeKnowledgeAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<KnowledgeBaseDocument>(stream, JsonOptions, cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    private async Task<Dictionary<string, KnowledgeEntry>> LoadCacheAsync(DirectoryInfo projectDirectory, CancellationToken cancellationToken)
    {
        var cacheDirectory = new DirectoryInfo(Path.Combine(projectDirectory.FullName, ".cache"));
        if (!cacheDirectory.Exists)
        {
            cacheDirectory.Create();
        }

        var cachePath = Path.Combine(cacheDirectory.FullName, "mediawiki_cache.json");
        if (!File.Exists(cachePath))
        {
            return new Dictionary<string, KnowledgeEntry>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            await using var stream = File.OpenRead(cachePath);
            var cache = await JsonSerializer.DeserializeAsync<Dictionary<string, KnowledgeEntry>>(stream, JsonOptions, cancellationToken);
            return cache is null
                ? new Dictionary<string, KnowledgeEntry>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, KnowledgeEntry>(cache, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, KnowledgeEntry>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private async Task SaveCacheAsync(DirectoryInfo projectDirectory, Dictionary<string, KnowledgeEntry> cache, CancellationToken cancellationToken)
    {
        var cacheDirectory = new DirectoryInfo(Path.Combine(projectDirectory.FullName, ".cache"));
        if (!cacheDirectory.Exists)
        {
            cacheDirectory.Create();
        }

        var cachePath = Path.Combine(cacheDirectory.FullName, "mediawiki_cache.json");
        await using var stream = File.Create(cachePath);
        await JsonSerializer.SerializeAsync(stream, cache, JsonOptions, cancellationToken);
    }

    private async Task<KnowledgeEntry?> FetchEntryForTokenAsync(string apiBase, string token, CancellationToken cancellationToken)
    {
        try
        {
            var searchUrl = $"{apiBase}?action=opensearch&limit=1&namespace=0&format=json&search={Uri.EscapeDataString(token)}";
            using var searchResponse = await _httpClient.GetAsync(searchUrl, cancellationToken);
            if (!searchResponse.IsSuccessStatusCode)
            {
                return null;
            }

            await using var contentStream = await searchResponse.Content.ReadAsStreamAsync(cancellationToken);
            using var searchDocument = await JsonDocument.ParseAsync(contentStream, cancellationToken: cancellationToken);

            if (searchDocument.RootElement.ValueKind != JsonValueKind.Array || searchDocument.RootElement.GetArrayLength() < 4)
            {
                return null;
            }

            var titles = searchDocument.RootElement[1];
            var urls = searchDocument.RootElement[3];
            if (titles.GetArrayLength() == 0 || urls.GetArrayLength() == 0)
            {
                return null;
            }

            var title = titles[0].GetString();
            var url = urls[0].GetString();
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(url))
            {
                return null;
            }

            var summary = await FetchSummaryAsync(apiBase, title, cancellationToken);
            var conceptId = $"wiki:{ToSlug(title)}";

            var aliases = new List<string>();
            if (!string.Equals(title, token, StringComparison.OrdinalIgnoreCase))
            {
                aliases.Add(token);
            }

            var sourceUri = new Uri(url);
            var source = new KnowledgeSource(
                sourceUri.Host,
                url,
                "CC-BY-SA 3.0",
                DateTimeOffset.UtcNow);

            return new KnowledgeEntry(
                conceptId,
                title,
                summary,
                source,
                aliases.Count > 0 ? aliases : null);
        }
        catch
        {
            return null;
        }
    }

    private async Task<string> FetchSummaryAsync(string apiBase, string title, CancellationToken cancellationToken)
    {
        try
        {
            var parseUrl = $"{apiBase}?action=parse&page={Uri.EscapeDataString(title)}&prop=text&section=0&format=json&formatversion=2";
            using var parseResponse = await _httpClient.GetAsync(parseUrl, cancellationToken);
            if (!parseResponse.IsSuccessStatusCode)
            {
                return string.Empty;
            }

            await using var contentStream = await parseResponse.Content.ReadAsStreamAsync(cancellationToken);
            using var parseDocument = await JsonDocument.ParseAsync(contentStream, cancellationToken: cancellationToken);

            if (!parseDocument.RootElement.TryGetProperty("parse", out var parseElement) ||
                !parseElement.TryGetProperty("text", out var textElement))
            {
                return string.Empty;
            }

            var html = textElement.GetString();
            if (string.IsNullOrWhiteSpace(html))
            {
                return string.Empty;
            }

            var plain = HtmlTagRegex.Replace(html, " ");
            plain = Regex.Replace(plain, @"\s+", " ").Trim();
            if (plain.Length > 480)
            {
                plain = plain[..480].Trim() + "...";
            }

            return plain;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string ToSlug(string value)
    {
        var builder = new StringBuilder();
        foreach (var ch in value.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        var slug = builder.ToString().Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? "entry" : slug;
    }
}
