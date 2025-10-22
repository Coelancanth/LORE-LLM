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
using System.Security.Cryptography;
using LORE_LLM.Application.Retrieval;
using LORE_LLM.Domain.Extraction;

namespace LORE_LLM.Application.Wiki;

public sealed class WikiIndexService : IWikiIndexService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly IProjectNameSanitizer _projectNameSanitizer;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IDeterministicEmbeddingProvider _embeddingProvider;

    public WikiIndexService(IProjectNameSanitizer projectNameSanitizer, IHttpClientFactory httpClientFactory, IDeterministicEmbeddingProvider embeddingProvider)
    {
        _projectNameSanitizer = projectNameSanitizer;
        _httpClientFactory = httpClientFactory;
        _embeddingProvider = embeddingProvider;
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

            var redirectTargets = await TryGetRedirectTargetsAsync(file.FullName, cancellationToken);
            var isRedirect = redirectTargets is not null && redirectTargets.Count > 0;
            entries.Add(KnowledgeKeywordIndexEntry.FromTitle(title, isRedirect, redirectTargets));
        }

        var index = new KnowledgeKeywordIndex(DateTimeOffset.UtcNow, entries);
        await using var stream = File.Create(indexPath);
        await JsonSerializer.SerializeAsync(stream, index, JsonOptions, cancellationToken);
        return Result.Success(entries.Count);
    }

    public async Task<Result<int>> BuildRetrievalIndexesAsync(WikiIndexBuildOptions options, CancellationToken cancellationToken)
    {
        var keywordResult = await BuildKeywordIndexAsync(options.Workspace, options.ProjectDisplayName, options.ForceRefresh, cancellationToken);
        if (keywordResult.IsFailure)
        {
            return keywordResult;
        }

        var sanitizedProject = _projectNameSanitizer.Sanitize(options.ProjectDisplayName);
        var projectDirectory = new DirectoryInfo(Path.Combine(options.Workspace.FullName, sanitizedProject));
        if (!projectDirectory.Exists)
        {
            return Result.Failure<int>($"Project directory not found: {projectDirectory.FullName}");
        }

        var knowledgeDirectory = new DirectoryInfo(Path.Combine(projectDirectory.FullName, "knowledge"));
        if (!knowledgeDirectory.Exists)
        {
            knowledgeDirectory.Create();
        }

        var manifestPath = Path.Combine(knowledgeDirectory.FullName, "index.manifest.json");

        var providers = new List<RetrievalProviderInfo>();

        var keywordRelative = Path.Combine("knowledge", "wiki_keyword_index.json").Replace('\\', '/');
        var keywordPath = Path.Combine(projectDirectory.FullName, keywordRelative);
        string? keywordHash = null;
        if (File.Exists(keywordPath))
        {
            await using var kstream = File.OpenRead(keywordPath);
            var khash = await SHA256.HashDataAsync(kstream, cancellationToken);
            keywordHash = Convert.ToHexString(khash).ToLowerInvariant();
        }

        var keywordConfig = new Dictionary<string, JsonElement>
        {
            ["tokenizer"] = JsonSerializer.SerializeToElement("default"),
            ["minTokenLength"] = JsonSerializer.SerializeToElement(3)
        };
        providers.Add(new RetrievalProviderInfo("keyword", keywordRelative, keywordHash, keywordConfig));

        if (options.WithVector)
        {
            var vectorInfo = await BuildVectorIndexAsync(options, projectDirectory, cancellationToken);
            if (vectorInfo.IsFailure)
            {
                return Result.Failure<int>(vectorInfo.Error);
            }

            providers.Add(vectorInfo.Value);
        }

        var manifest = new RetrievalIndexManifest(DateTimeOffset.UtcNow, providers);
        await using (var stream = File.Create(manifestPath))
        {
            await JsonSerializer.SerializeAsync(stream, manifest, JsonOptions, cancellationToken);
        }

        await UpdateWorkspaceManifestAsync(projectDirectory, cancellationToken);

        return Result.Success(providers.Count);
    }

    private async Task<Result<RetrievalProviderInfo>> BuildVectorIndexAsync(WikiIndexBuildOptions options, DirectoryInfo projectDirectory, CancellationToken cancellationToken)
    {
        var config = new Dictionary<string, JsonElement>
        {
            ["endpoint"] = JsonSerializer.SerializeToElement(options.QdrantEndpoint),
            ["collection"] = JsonSerializer.SerializeToElement(options.QdrantCollection),
            ["dimensions"] = JsonSerializer.SerializeToElement(options.VectorDimension),
            ["embeddingSource"] = JsonSerializer.SerializeToElement(options.EmbeddingSource)
        };

        var http = _httpClientFactory.CreateClient("qdrant");
        var qdrant = new QdrantClient(http, options.QdrantEndpoint, options.QdrantApiKey);
        var ensure = await qdrant.EnsureCollectionAsync(options.QdrantCollection, options.VectorDimension, cancellationToken);
        if (ensure.IsFailure)
        {
            return Result.Failure<RetrievalProviderInfo>(ensure.Error);
        }

        // Index titles only for now; can be extended to include summary in the future.
        var keywordIndexPath = Path.Combine(projectDirectory.FullName, "knowledge", "wiki_keyword_index.json");
        var keywordIndex = await DeserializeKeywordIndexAsync(keywordIndexPath, cancellationToken);
        if (keywordIndex is null)
        {
            return Result.Failure<RetrievalProviderInfo>("Keyword index is required to seed vector index.");
        }

        var points = new List<(string Id, float[] Vector, object? Payload)>();
        foreach (var entry in keywordIndex.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var vector = _embeddingProvider.Embed(entry.Title, options.VectorDimension);
            points.Add((entry.Title, vector, new { title = entry.Title }));
        }

        var upsert = await qdrant.UpsertPointsAsync(options.QdrantCollection, points, cancellationToken);
        if (upsert.IsFailure)
        {
            return Result.Failure<RetrievalProviderInfo>(upsert.Error);
        }

        var info = new RetrievalProviderInfo("vector:qdrant", null, null, config);
        return Result.Success(info);
    }

    private static async Task UpdateWorkspaceManifestAsync(DirectoryInfo projectDirectory, CancellationToken cancellationToken)
    {
        var manifestPath = Path.Combine(projectDirectory.FullName, "workspace.json");
        if (!File.Exists(manifestPath))
        {
            return;
        }

        WorkspaceManifest? manifest;
        await using (var stream = File.OpenRead(manifestPath))
        {
            manifest = await JsonSerializer.DeserializeAsync<WorkspaceManifest>(stream, JsonOptions, cancellationToken);
        }

        if (manifest is null)
        {
            return;
        }

        var artifacts = new Dictionary<string, string>(manifest.Artifacts, StringComparer.OrdinalIgnoreCase);
        var indexManifestRelative = Path.Combine("knowledge", "index.manifest.json").Replace('\\', '/');
        artifacts["retrievalIndexManifest"] = indexManifestRelative;

        var updated = new WorkspaceManifest(
            GeneratedAt: DateTimeOffset.UtcNow,
            Project: manifest.Project,
            ProjectDisplayName: manifest.ProjectDisplayName,
            InputPath: manifest.InputPath,
            InputHash: manifest.InputHash,
            Artifacts: artifacts);

        await using var outStream = File.Create(manifestPath);
        await JsonSerializer.SerializeAsync(outStream, updated, JsonOptions, cancellationToken);
    }

    private static async Task<KnowledgeKeywordIndex?> DeserializeKeywordIndexAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            await using var stream = File.OpenRead(path);
            var index = await JsonSerializer.DeserializeAsync<KnowledgeKeywordIndex>(stream, JsonOptions, cancellationToken);
            return index;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<IReadOnlyList<KnowledgeRedirectTarget>?> TryGetRedirectTargetsAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            var text = await File.ReadAllTextAsync(path, cancellationToken);
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            // Redirect-only files produced by the crawler start with a small header and a list:
            // "Redirect to:\n\n- [Target](link)" and no additional content.
            // We consider a doc a redirect if it contains a heading or quote metadata then a line starting with "Redirect to:" and no other body paragraphs.

            var normalized = text.Replace("\r\n", "\n");
            var lines = normalized.Split('\n');
            var hasRedirectHeader = lines.Any(l => l.Trim().Equals("Redirect to:", StringComparison.OrdinalIgnoreCase));
            if (!hasRedirectHeader)
            {
                return null;
            }

            // Heuristic: redirect docs typically have 1-3 metadata lines, the redirect header, and 1-5 list items, with no other content.
            var nonEmpty = lines.Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
            var nonMeta = nonEmpty.Where(l => !l.StartsWith("> ") && !l.StartsWith("# ")).ToList();
            var redirectSection = nonMeta.SkipWhile(l => !l.Equals("Redirect to:", StringComparison.OrdinalIgnoreCase)).ToList();

            if (redirectSection.Count == 0)
            {
                return null;
            }

            // After "Redirect to:", expect only list items (links)
            var idx = redirectSection.FindIndex(l => l.Equals("Redirect to:", StringComparison.OrdinalIgnoreCase));
            var after = idx >= 0 && idx + 1 < redirectSection.Count ? redirectSection.Skip(idx + 1).ToList() : new List<string>();
            var allLists = after.All(l => l.StartsWith("- "));
            var hasAtLeastOneTarget = after.Any(l => l.StartsWith("- "));

            if (!(allLists && hasAtLeastOneTarget))
            {
                return null;
            }

            // Parse list items: "- [Display](link)"
            var targets = new List<KnowledgeRedirectTarget>();
            foreach (var line in after)
            {
                if (!line.StartsWith("- "))
                {
                    continue;
                }
                var trimmed = line.Substring(2).Trim();
                // Very light-weight parse of Markdown link
                var openBracket = trimmed.IndexOf('[');
                var closeBracket = trimmed.IndexOf(']');
                var openParen = trimmed.IndexOf('(');
                var closeParen = trimmed.LastIndexOf(')');
                if (openBracket >= 0 && closeBracket > openBracket && openParen > closeBracket && closeParen > openParen)
                {
                    var display = trimmed.Substring(openBracket + 1, closeBracket - openBracket - 1).Trim();
                    var href = trimmed.Substring(openParen + 1, closeParen - openParen - 1).Trim();
                    var slug = Path.GetFileNameWithoutExtension(href).Trim();
                    if (!string.IsNullOrWhiteSpace(display) && !string.IsNullOrWhiteSpace(slug))
                    {
                        targets.Add(new KnowledgeRedirectTarget(display, slug));
                    }
                }
            }

            return targets.Count > 0 ? targets : null;
        }
        catch
        {
            return null;
        }
    }
}


