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
    private const int MaxTokens = 50;
    private const int MaxKeywordCandidates = 30;

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
        bool offline,
        CancellationToken cancellationToken)
    {
        var knowledgePath = Path.Combine(projectDirectory.FullName, "knowledge_base.json");

        if (offline)
        {
            if (forceRefresh)
            {
                return Result.Failure<KnowledgeBaseDocument>("Cannot force-refresh wiki knowledge while offline.");
            }

            if (!File.Exists(knowledgePath))
            {
                return Result.Failure<KnowledgeBaseDocument>("Knowledge base not available offline. Run the investigation command online first.");
            }

            var cachedOffline = await DeserializeKnowledgeAsync(knowledgePath, cancellationToken);
            return cachedOffline is null
                ? Result.Failure<KnowledgeBaseDocument>("Cached knowledge base could not be read. Regenerate it while online.")
                : Result.Success(cachedOffline);
        }

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
            .Take(MaxTokens)
            .ToList();

        var cache = await LoadCacheAsync(projectDirectory, cancellationToken);
        var cacheUpdated = false;
        var entries = new List<KnowledgeEntry>();
        var processedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var keywordIndex = await EnsureKeywordIndexAsync(projectDirectory, apiBase, forceRefresh, cancellationToken);
        var candidateTitles = GetCandidateTitles(keywordIndex, tokens);

        foreach (var title in candidateTitles)
        {
            if (!processedKeys.Add(title))
            {
                continue;
            }

            if (TryGetCachedEntry(cache, title, out var cachedEntry))
            {
                entries.Add(cachedEntry);
                continue;
            }

            var entry = await FetchEntryForTitleAsync(apiBase, title, cancellationToken);
            if (entry is null)
            {
                continue;
            }

            entries.Add(entry);
            CacheEntry(cache, entry, title);
            cacheUpdated = true;
        }

        foreach (var token in tokens)
        {
            if (!processedKeys.Add(token))
            {
                continue;
            }

            if (TryGetCachedEntry(cache, token, out var cachedEntry))
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
            CacheEntry(cache, entry, token);
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

    private async Task<KnowledgeKeywordIndex?> EnsureKeywordIndexAsync(DirectoryInfo projectDirectory, string apiBase, bool forceRefresh, CancellationToken cancellationToken)
    {
        var knowledgeDirectory = new DirectoryInfo(Path.Combine(projectDirectory.FullName, "knowledge"));
        if (!knowledgeDirectory.Exists)
        {
            knowledgeDirectory.Create();
        }

        var indexPath = Path.Combine(knowledgeDirectory.FullName, "wiki_keyword_index.json");
        if (!forceRefresh && File.Exists(indexPath))
        {
            var cached = await DeserializeKeywordIndexAsync(indexPath, cancellationToken);
            if (cached is not null)
            {
                return cached;
            }
        }

        var entries = await FetchAllPagesAsync(apiBase, cancellationToken);
        if (entries.Count == 0)
        {
            return null;
        }

        var index = new KnowledgeKeywordIndex(DateTimeOffset.UtcNow, entries);
        await using (var stream = File.Create(indexPath))
        {
            await JsonSerializer.SerializeAsync(stream, index, JsonOptions, cancellationToken);
        }

        return index;
    }

    private async Task<KnowledgeKeywordIndex?> DeserializeKeywordIndexAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<KnowledgeKeywordIndex>(stream, JsonOptions, cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    private IReadOnlyList<string> GetCandidateTitles(KnowledgeKeywordIndex? index, IReadOnlyList<string> tokens)
    {
        if (index is null)
        {
            return Array.Empty<string>();
        }

        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var token in tokens)
        {
            foreach (var title in index.Lookup(token))
            {
                if (!string.IsNullOrWhiteSpace(title))
                {
                    candidates.Add(title);
                }
            }

            if (candidates.Count >= MaxKeywordCandidates)
            {
                break;
            }
        }

        return candidates.Take(MaxKeywordCandidates).ToList();
    }

    private async Task<List<KnowledgeKeywordIndexEntry>> FetchAllPagesAsync(string apiBase, CancellationToken cancellationToken)
    {
        var entries = new List<KnowledgeKeywordIndexEntry>();
        string? continuation = null;

        do
        {
            var url = new StringBuilder($"{apiBase}?action=query&list=allpages&format=json&formatversion=2&aplimit=500");
            if (!string.IsNullOrWhiteSpace(continuation))
            {
                url.Append($"&apcontinue={Uri.EscapeDataString(continuation)}");
            }

            using var response = await _httpClient.GetAsync(url.ToString(), cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                break;
            }

            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(contentStream, cancellationToken: cancellationToken);

            if (!document.RootElement.TryGetProperty("query", out var queryElement) ||
                !queryElement.TryGetProperty("allpages", out var allPagesElement) ||
                allPagesElement.ValueKind != JsonValueKind.Array)
            {
                break;
            }

            foreach (var page in allPagesElement.EnumerateArray())
            {
                if (page.TryGetProperty("title", out var titleElement))
                {
                    var title = titleElement.GetString();
                    if (!string.IsNullOrWhiteSpace(title))
                    {
                        entries.Add(KnowledgeKeywordIndexEntry.FromTitle(title));
                    }
                }
            }

            continuation = document.RootElement.TryGetProperty("continue", out var continueElement) &&
                           continueElement.TryGetProperty("apcontinue", out var continueToken)
                ? continueToken.GetString()
                : null;
        }
        while (!string.IsNullOrWhiteSpace(continuation) && entries.Count < 5000);

        return entries;
    }

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

    private static bool TryGetCachedEntry(Dictionary<string, KnowledgeEntry> cache, string key, out KnowledgeEntry entry)
    {
        if (cache.TryGetValue(key, out var value))
        {
            entry = value;
            return true;
        }

        entry = default!;
        return false;
    }

    private static void CacheEntry(Dictionary<string, KnowledgeEntry> cache, KnowledgeEntry entry, string primaryKey)
    {
        cache[primaryKey] = entry;
        cache[entry.Title] = entry;
        cache[entry.ConceptId] = entry;

        if (entry.Aliases is null)
        {
            return;
        }

        foreach (var alias in entry.Aliases)
        {
            cache[alias] = entry;
        }
    }

    private async Task<KnowledgeEntry?> FetchEntryForTitleAsync(string apiBase, string title, CancellationToken cancellationToken)
    {
        try
        {
            var summary = await FetchSummaryAsync(apiBase, title, cancellationToken);
            var slug = TextSlugger.ToSlug(title);
            var baseUri = new Uri(apiBase);
            var pageUrl = $"{baseUri.Scheme}://{baseUri.Host}/wiki/{Uri.EscapeDataString(title.Replace(' ', '_'))}";

            var source = new KnowledgeSource(
                baseUri.Host,
                pageUrl,
                "CC-BY-SA 3.0",
                DateTimeOffset.UtcNow);

            return new KnowledgeEntry(
                $"wiki:{slug}",
                title,
                summary,
                source,
                null);
        }
        catch
        {
            return null;
        }
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
            var conceptId = $"wiki:{TextSlugger.ToSlug(title)}";

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

}
