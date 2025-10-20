using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using LORE_LLM.Application.PostProcessing;
using LORE_LLM.Domain.Knowledge;

namespace LORE_LLM.Application.Wiki;

public sealed class WikiIndexService : IWikiIndexService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly IProjectNameSanitizer _projectNameSanitizer;

    public WikiIndexService(IProjectNameSanitizer projectNameSanitizer)
    {
        _projectNameSanitizer = projectNameSanitizer;
    }

    public async Task<Result<int>> BuildKeywordIndexAsync(DirectoryInfo workspace, string projectDisplayName, bool forceRefresh, CancellationToken cancellationToken)
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

        var rawDirectory = new DirectoryInfo(Path.Combine(projectDirectory.FullName, "knowledge", "raw"));
        if (!rawDirectory.Exists)
        {
            return Result.Failure<int>("No wiki raw markdown directory found. Run crawl-wiki first.");
        }

        var indexDirectory = new DirectoryInfo(Path.Combine(projectDirectory.FullName, "knowledge"));
        if (!indexDirectory.Exists)
        {
            indexDirectory.Create();
        }

        var indexPath = Path.Combine(indexDirectory.FullName, "wiki_keyword_index.json");
        if (!forceRefresh && File.Exists(indexPath))
        {
            return Result.Success(0);
        }

        var entries = new List<KnowledgeKeywordIndexEntry>();
        foreach (var file in rawDirectory.EnumerateFiles("*.md", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var title = Path.GetFileNameWithoutExtension(file.Name)
                .Replace('-', ' ')
                .Replace('_', ' ')
                .Trim();

            if (string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            var isRedirect = await IsRedirectDocumentAsync(file.FullName, cancellationToken);
            entries.Add(KnowledgeKeywordIndexEntry.FromTitle(title, isRedirect));
        }

        var index = new KnowledgeKeywordIndex(DateTimeOffset.UtcNow, entries);
        await using var stream = File.Create(indexPath);
        await JsonSerializer.SerializeAsync(stream, index, JsonOptions, cancellationToken);
        return Result.Success(entries.Count);
    }

    private static async Task<bool> IsRedirectDocumentAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            var text = await File.ReadAllTextAsync(path, cancellationToken);
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            // Redirect-only files produced by the crawler start with a small header and a list:
            // "Redirect to:\n\n- [Target](link)" and no additional content.
            // We consider a doc a redirect if it contains a heading or quote metadata then a line starting with "Redirect to:" and no other body paragraphs.

            var normalized = text.Replace("\r\n", "\n");
            var lines = normalized.Split('\n');
            var hasRedirectHeader = lines.Any(l => l.Trim().Equals("Redirect to:", StringComparison.OrdinalIgnoreCase));
            if (!hasRedirectHeader)
            {
                return false;
            }

            // Heuristic: redirect docs typically have 1-3 metadata lines, the redirect header, and 1-5 list items, with no other content.
            var nonEmpty = lines.Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
            var nonMeta = nonEmpty.Where(l => !l.StartsWith("> ") && !l.StartsWith("# ")).ToList();
            var redirectSection = nonMeta.SkipWhile(l => !l.Equals("Redirect to:", StringComparison.OrdinalIgnoreCase)).ToList();

            if (redirectSection.Count == 0)
            {
                return false;
            }

            // After "Redirect to:", expect only list items (links)
            var idx = redirectSection.FindIndex(l => l.Equals("Redirect to:", StringComparison.OrdinalIgnoreCase));
            var after = idx >= 0 && idx + 1 < redirectSection.Count ? redirectSection.Skip(idx + 1).ToList() : new List<string>();
            var allLists = after.All(l => l.StartsWith("- "));
            var hasAtLeastOneTarget = after.Any(l => l.StartsWith("- "));

            return allLists && hasAtLeastOneTarget;
        }
        catch
        {
            return false;
        }
    }
}


