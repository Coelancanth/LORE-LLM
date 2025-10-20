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
using LORE_LLM.Application.Investigation;
using LORE_LLM.Application.PostProcessing;
using ReverseMarkdown;

namespace LORE_LLM.Application.Wiki;

public sealed class MediaWikiCrawler : IMediaWikiCrawler
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly Converter _markdownConverter = new Converter();
    private readonly IProjectNameSanitizer _projectNameSanitizer;

    public MediaWikiCrawler(HttpClient httpClient, IProjectNameSanitizer projectNameSanitizer)
    {
        _httpClient = httpClient;
        _projectNameSanitizer = projectNameSanitizer;
    }

    public async Task<Result<int>> CrawlAsync(
        DirectoryInfo workspace,
        string projectDisplayName,
        bool forceRefresh,
        string[]? specificPages,
        int maxPages,
        CancellationToken cancellationToken)
    {
        if (!workspace.Exists)
        {
            return Result.Failure<int>("Workspace directory not found.");
        }

        var sanitizedProject = _projectNameSanitizer.Sanitize(projectDisplayName);
        var projectDirectory = new DirectoryInfo(Path.Combine(workspace.FullName, sanitizedProject));
        if (!projectDirectory.Exists)
        {
            return Result.Failure<int>($"Project directory not found: {projectDirectory.FullName}");
        }

        var apiBase = ResolveApiBase(sanitizedProject);
        if (apiBase is null)
        {
            return Result.Failure<int>($"No MediaWiki configuration available for project '{sanitizedProject}'.");
        }

        var targetDirectory = new DirectoryInfo(Path.Combine(projectDirectory.FullName, "knowledge", "raw"));
        if (!targetDirectory.Exists)
        {
            targetDirectory.Create();
        }

        var titles = specificPages is { Length: > 0 }
            ? new List<string>(specificPages)
            : await FetchAllPageTitlesAsync(apiBase, cancellationToken);

        if (titles.Count == 0)
        {
            return Result.Failure<int>("No wiki pages discovered.");
        }

        if (maxPages > 0)
        {
            titles = titles.Take(maxPages).ToList();
        }

        var processed = 0;
        foreach (var title in titles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await CrawlSinglePageAsync(apiBase, title, targetDirectory, forceRefresh, cancellationToken);
            if (result)
            {
                processed++;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(350), cancellationToken);
        }

        return Result.Success(processed);
    }

    private static string? ResolveApiBase(string sanitizedProject) => sanitizedProject switch
    {
        "pathologic2-marble-nest" => "https://pathologic.fandom.com/api.php",
        _ => null
    };

    private async Task<List<string>> FetchAllPageTitlesAsync(string apiBase, CancellationToken cancellationToken)
    {
        var titles = new List<string>();
        string? continuation = null;

        do
        {
            var url = new StringBuilder($"{apiBase}?action=query&list=allpages&format=json&formatversion=2&aplimit=500");
            if (!string.IsNullOrWhiteSpace(continuation))
            {
                url.Append("&apcontinue=").Append(Uri.EscapeDataString(continuation));
            }

            using var response = await _httpClient.GetAsync(url.ToString(), cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                break;
            }

            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(contentStream, cancellationToken: cancellationToken);

            if (!document.RootElement.TryGetProperty("query", out var queryElement) ||
                !queryElement.TryGetProperty("allpages", out var pagesElement))
            {
                break;
            }

            foreach (var page in pagesElement.EnumerateArray())
            {
                if (page.TryGetProperty("title", out var titleElement))
                {
                    var title = titleElement.GetString();
                    if (!string.IsNullOrWhiteSpace(title))
                    {
                        titles.Add(title);
                    }
                }
            }

            continuation = document.RootElement.TryGetProperty("continue", out var continueElement) &&
                           continueElement.TryGetProperty("apcontinue", out var apContinue)
                ? apContinue.GetString()
                : null;
        }
        while (!string.IsNullOrWhiteSpace(continuation));

        return titles;
    }

    private async Task<bool> CrawlSinglePageAsync(
        string apiBase,
        string title,
        DirectoryInfo targetDirectory,
        bool forceRefresh,
        CancellationToken cancellationToken)
    {
        var slug = TextSlugger.ToSlug(title);
        var outputPath = Path.Combine(targetDirectory.FullName, $"{slug}.md");
        if (!forceRefresh && File.Exists(outputPath))
        {
            return false;
        }

        var parseUrl = $"{apiBase}?action=parse&page={Uri.EscapeDataString(title)}&prop=text|revid&format=json&formatversion=2";
        using var response = await _httpClient.GetAsync(parseUrl, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return false;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty("parse", out var parseElement) ||
            !parseElement.TryGetProperty("text", out var textElement))
        {
            return false;
        }

        var html = textElement.GetString();
        if (string.IsNullOrWhiteSpace(html))
        {
            return false;
        }

        var markdownBody = _markdownConverter.Convert(html);
        markdownBody = Regex.Replace(markdownBody, "<!--.*?-->", string.Empty, RegexOptions.Singleline).Trim();
        var baseUri = new Uri(apiBase);
        var pageUrl = $"{baseUri.Scheme}://{baseUri.Host}/wiki/{Uri.EscapeDataString(title.Replace(' ', '_'))}";

        var builder = new StringBuilder();
        builder.AppendLine($"# {title}");
        builder.AppendLine();
        builder.AppendLine($"> Source: {pageUrl}");
        builder.AppendLine($"> License: CC-BY-SA 3.0");
        builder.AppendLine($"> Retrieved: {DateTimeOffset.UtcNow:O}");
        builder.AppendLine();
        builder.AppendLine(markdownBody);
        builder.AppendLine();

        await File.WriteAllTextAsync(outputPath, builder.ToString(), cancellationToken);
        return true;
    }
}




