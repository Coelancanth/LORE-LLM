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
using Microsoft.Extensions.Options;
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
    private readonly MediaWikiHtmlPostProcessingPipeline _htmlPipeline;
    private readonly MediaWikiCrawlerOptions _options;

    public MediaWikiCrawler(
        HttpClient httpClient,
        IProjectNameSanitizer projectNameSanitizer,
        MediaWikiHtmlPostProcessingPipeline htmlPipeline,
        IOptions<MediaWikiCrawlerOptions> options)
    {
        _httpClient = httpClient;
        _projectNameSanitizer = projectNameSanitizer;
        _htmlPipeline = htmlPipeline;
        _options = options.Value;
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

        var projectOptions = _options.GetProjectOptions(sanitizedProject);
        if (projectOptions is null || string.IsNullOrWhiteSpace(projectOptions.ApiBase))
        {
            return Result.Failure<int>($"No MediaWiki configuration available for project '{sanitizedProject}'.");
        }
        var apiBase = projectOptions.ApiBase;

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
            var result = await CrawlSinglePageAsync(
                apiBase,
                sanitizedProject,
                projectOptions,
                title,
                targetDirectory,
                forceRefresh,
                cancellationToken);
            if (result)
            {
                processed++;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(350), cancellationToken);
        }

        return Result.Success(processed);
    }

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
        string sanitizedProject,
        MediaWikiCrawlerProjectOptions projectOptions,
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

        var configuredProcessors = projectOptions.HtmlPostProcessors;
        var processedResult = _htmlPipeline.Process(sanitizedProject, title, html, configuredProcessors);
        var markdownBody = ConvertHtmlToMarkdown(processedResult.Html);
        if (processedResult.RedirectTargets.Count > 0)
        {
            var redirectMarkdown = BuildRedirectMarkdown(processedResult.RedirectTargets);
            if (string.IsNullOrWhiteSpace(markdownBody))
            {
                markdownBody = redirectMarkdown;
            }
            else
            {
                markdownBody = $"{redirectMarkdown}{Environment.NewLine}{Environment.NewLine}{markdownBody}".Trim();
            }
        }
        var baseUri = new Uri(apiBase);
        var pageUrl = $"{baseUri.Scheme}://{baseUri.Host}/wiki/{Uri.EscapeDataString(title.Replace(' ', '_'))}";
        var retrievedAt = DateTimeOffset.UtcNow;

        var builder = new StringBuilder();
        builder.AppendLine($"# {title}");
        builder.AppendLine();
        builder.AppendLine($"> Source: {pageUrl}");
        builder.AppendLine($"> License: CC-BY-SA 3.0");
        builder.AppendLine($"> Retrieved: {retrievedAt:O}");
        builder.AppendLine();
        builder.AppendLine(markdownBody);
        builder.AppendLine();

        await File.WriteAllTextAsync(outputPath, builder.ToString(), cancellationToken);

        if (projectOptions.TabOutputs.Count > 0 && processedResult.TabSections.Count > 0)
        {
            await WriteTabVariantsAsync(
                projectOptions,
                processedResult.TabSections,
                slug,
                title,
                pageUrl,
                retrievedAt,
                targetDirectory,
                cancellationToken);
        }

        return true;
    }

    private async Task WriteTabVariantsAsync(
        MediaWikiCrawlerProjectOptions projectOptions,
        IReadOnlyList<MediaWikiTabSection> tabSections,
        string baseSlug,
        string pageTitle,
        string pageUrl,
        DateTimeOffset retrievedAt,
        DirectoryInfo targetDirectory,
        CancellationToken cancellationToken)
    {
        foreach (var tabOutput in projectOptions.TabOutputs)
        {
            var section = FindMatchingSection(tabSections, tabOutput);
            if (section is null)
            {
                continue;
            }

            var markdown = ConvertHtmlToMarkdown(section.Html);
            markdown = StripLeadingHeading(markdown, section.Name).Trim();

            var heading = BuildTabHeading(pageTitle, section.Name, tabOutput);

            var builder = new StringBuilder();
            builder.AppendLine($"# {heading}");
            builder.AppendLine();
            builder.AppendLine($"> Source: {pageUrl}");
            builder.AppendLine($"> License: CC-BY-SA 3.0");
            builder.AppendLine($"> Variant: {section.Name}");
            builder.AppendLine($"> Retrieved: {retrievedAt:O}");
            builder.AppendLine();

            if (!string.IsNullOrWhiteSpace(markdown))
            {
                builder.AppendLine(markdown);
                builder.AppendLine();
            }

            var fileName = BuildTabFileName(baseSlug, section, tabOutput);
            var outputPath = Path.Combine(targetDirectory.FullName, fileName);
            await File.WriteAllTextAsync(outputPath, builder.ToString(), cancellationToken);
        }
    }

    private string ConvertHtmlToMarkdown(string html)
    {
        var markdownBody = _markdownConverter.Convert(html);
        return Regex.Replace(markdownBody, "<!--.*?-->", string.Empty, RegexOptions.Singleline).Trim();
    }

    private static string StripLeadingHeading(string markdown, string heading)
    {
        if (string.IsNullOrWhiteSpace(markdown) || string.IsNullOrWhiteSpace(heading))
        {
            return markdown;
        }

        var pattern = $"^##\\s*{Regex.Escape(heading)}\\s*\\r?\\n";
        return Regex.Replace(markdown, pattern, string.Empty, RegexOptions.IgnoreCase);
    }

    private static string BuildTabHeading(string pageTitle, string tabName, MediaWikiTabOutputOptions tabOutput)
    {
        if (!string.IsNullOrWhiteSpace(tabOutput.TitleFormat))
        {
            var heading = tabOutput.TitleFormat;
            heading = heading.Replace("{title}", pageTitle);
            heading = heading.Replace("{tab}", tabName);
            return heading;
        }

        return $"{pageTitle} ({tabName})";
    }

    private static string BuildTabFileName(string baseSlug, MediaWikiTabSection section, MediaWikiTabOutputOptions tabOutput)
    {
        var suffix = tabOutput.FileSuffix;
        if (string.IsNullOrWhiteSpace(suffix))
        {
            suffix = "-" + section.Slug;
        }
        else
        {
            suffix = NormalizeSuffix(suffix, section.Slug);
        }

        return $"{baseSlug}{suffix}.md";
    }

    private static string BuildRedirectMarkdown(IReadOnlyList<MediaWikiRedirectTarget> targets)
    {
        if (targets.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.AppendLine("Redirect to:");
        builder.AppendLine();

        foreach (var target in targets)
        {
            var link = ResolveRedirectLink(target);
            builder.AppendLine($"- [{target.DisplayText}]({link})");
        }

        return builder.ToString().TrimEnd();
    }

    private static string ResolveRedirectLink(MediaWikiRedirectTarget target)
    {
        if (string.IsNullOrWhiteSpace(target.Href))
        {
            return target.DisplayText;
        }

        if (Uri.TryCreate(target.Href, UriKind.Absolute, out var absoluteUri))
        {
            return absoluteUri.ToString();
        }

        if (target.Href.StartsWith("/wiki/", StringComparison.OrdinalIgnoreCase))
        {
            var page = target.Href.Substring("/wiki/".Length);
            page = Uri.UnescapeDataString(page);
            page = page.Replace('_', ' ');
            var slug = TextSlugger.ToSlug(page);
            return $"{slug}.md";
        }

        return target.Href;
    }

    private static string NormalizeSuffix(string suffix, string defaultSlug)
    {
        var trimmed = suffix.Trim();
        if (trimmed.Length == 0)
        {
            return "-" + defaultSlug;
        }

        if (!trimmed.StartsWith("-", StringComparison.Ordinal))
        {
            trimmed = "-" + trimmed.TrimStart('-');
        }

        return trimmed;
    }

    private static MediaWikiTabSection? FindMatchingSection(
        IReadOnlyList<MediaWikiTabSection> sections,
        MediaWikiTabOutputOptions tabOutput)
    {
        foreach (var section in sections)
        {
            if (!string.IsNullOrWhiteSpace(tabOutput.TabName) &&
                string.Equals(section.Name, tabOutput.TabName, StringComparison.OrdinalIgnoreCase))
            {
                return section;
            }

            if (!string.IsNullOrWhiteSpace(tabOutput.TabSlug) &&
                string.Equals(section.Slug, tabOutput.TabSlug, StringComparison.OrdinalIgnoreCase))
            {
                return section;
            }
        }

        return null;
    }
}




