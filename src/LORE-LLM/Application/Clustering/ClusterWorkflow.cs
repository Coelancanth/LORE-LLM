using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using LORE_LLM.Application.Chat;
using LORE_LLM.Application.Commands.Cluster;
using LORE_LLM.Application.PostProcessing;
using LORE_LLM.Domain.Clusters;
using LORE_LLM.Domain.Extraction;

namespace LORE_LLM.Application.Clustering;

public sealed class ClusterWorkflow : IClusterWorkflow
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly ChatProviderResolver _chatProviderResolver;
    private readonly IProjectNameSanitizer _projectNameSanitizer;

    public ClusterWorkflow(ChatProviderResolver chatProviderResolver, IProjectNameSanitizer projectNameSanitizer)
    {
        _chatProviderResolver = chatProviderResolver;
        _projectNameSanitizer = projectNameSanitizer;
    }

    public async Task<Result<int>> RunAsync(ClusterCommandOptions options, CancellationToken cancellationToken)
    {
        if (options.Workspace is null || !options.Workspace.Exists)
        {
            return Result.Failure<int>("Workspace directory not found.");
        }

        var sanitizedProject = _projectNameSanitizer.Sanitize(options.Project);
        var projectPath = Path.Combine(options.Workspace.FullName, sanitizedProject);
        var projectDirectory = new DirectoryInfo(projectPath);
        if (!projectDirectory.Exists)
        {
            return Result.Failure<int>($"Project directory not found: {projectDirectory.FullName}");
        }

        var sourceTextPath = Path.Combine(projectDirectory.FullName, "source_text_raw.json");
        if (!File.Exists(sourceTextPath))
        {
            return Result.Failure<int>($"Required artifact missing: {sourceTextPath}");
        }

        var sourceDocument = await DeserializeAsync<SourceTextRawDocument>(sourceTextPath, cancellationToken);
        if (sourceDocument is null)
        {
            return Result.Failure<int>("Failed to deserialize source_text_raw.json.");
        }

        var provider = _chatProviderResolver.Resolve(options.Provider);
        if (provider is null)
        {
            return Result.Failure<int>($"Chat provider not found: {options.Provider}");
        }

        var promptTemplate = options.PromptTemplate is not null && options.PromptTemplate.Exists
            ? await File.ReadAllTextAsync(options.PromptTemplate.FullName, cancellationToken)
            : DefaultPromptTemplate;

        var segments = sourceDocument.Segments
            .Where(s => options.IncludeEmpty || !s.IsEmpty)
            .ToList();

        if (options is { MaxSegments: > 0 } && segments.Count > options.MaxSegments)
        {
            segments = segments.Take(options.MaxSegments).ToList();
        }

        // Optionally seed from precomputed clusters
        var transcript = new StringBuilder();
        var allClusters = new List<ClusterContext>();
        if (options.Precomputed != PrecomputedIngestMode.Ignore)
        {
            var precomputedPath = Path.Combine(projectDirectory.FullName, "clusters_precomputed.json");
            if (File.Exists(precomputedPath))
            {
                var pre = await DeserializeAsync<ClusterDocument>(precomputedPath, cancellationToken);
                if (pre is not null && pre.Clusters is not null)
                {
                    if (options.Precomputed == PrecomputedIngestMode.Accept)
                    {
                        allClusters.AddRange(pre.Clusters);
                        // Accept mode: skip LLM entirely and persist precomputed as-is
                        var clusterDocAccept = new ClusterDocument(
                            sourceDocument.Project,
                            sourceDocument.ProjectDisplayName,
                            DateTimeOffset.UtcNow,
                            sourceDocument.InputHash,
                            allClusters);
                        var clustersPathAccept = Path.Combine(projectDirectory.FullName, "clusters_llm.json");
                        await SerializeAsync(clustersPathAccept, clusterDocAccept, cancellationToken);

                        var manifestPathAccept = Path.Combine(projectDirectory.FullName, "workspace.json");
                        if (File.Exists(manifestPathAccept))
                        {
                            await UpdateManifestAsync(projectDirectory, manifestPathAccept, clusterDocAccept.GeneratedAt, cancellationToken);
                        }
                        return Result.Success(0);
                    }

                    // Seed: remove seeded segments from the LLM batches
                    var seededIds = new HashSet<string>(pre.Clusters.SelectMany(c => c.MemberIds));
                    segments = segments.Where(s => !seededIds.Contains(s.Id)).ToList();
                    allClusters.AddRange(pre.Clusters);
                }
            }
        }

        var batches = BatchSegments(segments, Math.Max(1, options.BatchSize));

        foreach (var batch in batches)
        {
            var prompt = BuildPrompt(promptTemplate, sourceDocument.ProjectDisplayName, batch);
            var responseResult = await provider.CompleteAsync(prompt, cancellationToken);
            transcript.AppendLine("# Prompt").AppendLine(prompt).AppendLine();
            if (responseResult.IsFailure)
            {
                return Result.Failure<int>(responseResult.Error);
            }

            var response = responseResult.Value;
            transcript.AppendLine("# Response").AppendLine(response).AppendLine();

            var parseResult = TryParseClusters(response);
            if (parseResult.IsFailure)
            {
                return Result.Failure<int>($"Failed to parse clusters from LLM response: {parseResult.Error}");
            }

            var clustersFromBatch = parseResult.Value;
            if (options.MaxClusters > 0)
            {
                var remaining = options.MaxClusters - allClusters.Count;
                if (remaining <= 0)
                {
                    break;
                }

                if (clustersFromBatch.Count > remaining)
                {
                    allClusters.AddRange(clustersFromBatch.Take(remaining));
                    break;
                }

                allClusters.AddRange(clustersFromBatch);
            }
            else
            {
                allClusters.AddRange(clustersFromBatch);
            }
        }

        var clusterDoc = new ClusterDocument(
            sourceDocument.Project,
            sourceDocument.ProjectDisplayName,
            DateTimeOffset.UtcNow,
            sourceDocument.InputHash,
            allClusters);

        var clustersPath = Path.Combine(projectDirectory.FullName, "clusters_llm.json");
        await SerializeAsync(clustersPath, clusterDoc, cancellationToken);

        if (options.SaveTranscript)
        {
            var transcriptPath = Path.Combine(projectDirectory.FullName, "clusters_llm_transcript.md");
            await File.WriteAllTextAsync(transcriptPath, transcript.ToString(), cancellationToken);
        }

        var manifestPath = Path.Combine(projectDirectory.FullName, "workspace.json");
        if (File.Exists(manifestPath))
        {
            await UpdateManifestAsync(projectDirectory, manifestPath, clusterDoc.GeneratedAt, cancellationToken);
        }

        return Result.Success(0);
    }

    private static string BuildPrompt(string template, string projectDisplayName, IReadOnlyList<SourceSegment> segments)
    {
        var formatted = new StringBuilder();
        formatted.AppendLine(template.Replace("{{projectDisplayName}}", projectDisplayName));
        formatted.AppendLine();
        formatted.AppendLine("Segments:");
        foreach (var s in segments)
        {
            formatted.AppendLine($"- id: {s.Id}");
            formatted.AppendLine($"  text: \"{s.Text.Replace("\"", "\\\"")}\"");
        }
        return formatted.ToString();
    }

    private static IReadOnlyList<IReadOnlyList<SourceSegment>> BatchSegments(IReadOnlyList<SourceSegment> segments, int batchSize)
    {
        var batches = new List<IReadOnlyList<SourceSegment>>();
        for (var i = 0; i < segments.Count; i += batchSize)
        {
            batches.Add(segments.Skip(i).Take(batchSize).ToList());
        }
        return batches;
    }

    private static Result<IReadOnlyList<ClusterContext>> TryParseClusters(string response)
    {
        // Attempt 1: full document shape
        try
        {
            var doc = JsonSerializer.Deserialize<ClusterDocument>(response, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            if (doc is not null && doc.Clusters is not null)
            {
                return Result.Success((IReadOnlyList<ClusterContext>)doc.Clusters);
            }
        }
        catch
        {
            // ignore and try next shape
        }

        // Attempt 2: bare array of clusters
        try
        {
            var clusters = JsonSerializer.Deserialize<List<ClusterContext>>(response, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            if (clusters is not null)
            {
                return Result.Success((IReadOnlyList<ClusterContext>)clusters);
            }
        }
        catch
        {
            // ignore and fall-through
        }

        return Result.Failure<IReadOnlyList<ClusterContext>>("No clusters found in response.");
    }

    private static async Task<T?> DeserializeAsync<T>(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken);
    }

    private static async Task SerializeAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, value, JsonOptions, cancellationToken);
    }

    private static async Task UpdateManifestAsync(DirectoryInfo projectDirectory, string manifestPath, DateTimeOffset generatedAt, CancellationToken cancellationToken)
    {
        WorkspaceManifest? manifest;
        await using (var manifestStream = File.OpenRead(manifestPath))
        {
            manifest = await JsonSerializer.DeserializeAsync<WorkspaceManifest>(manifestStream, JsonOptions, cancellationToken);
        }

        if (manifest is null)
        {
            return;
        }

        var artifacts = new Dictionary<string, string>(manifest.Artifacts, StringComparer.OrdinalIgnoreCase)
        {
            ["clustersLlm"] = "clusters_llm.json"
        };

        var updatedManifest = new WorkspaceManifest(
            generatedAt,
            manifest.Project,
            manifest.ProjectDisplayName,
            manifest.InputPath,
            manifest.InputHash,
            artifacts);

        await using var outputStream = File.Create(manifestPath);
        await JsonSerializer.SerializeAsync(outputStream, updatedManifest, JsonOptions, cancellationToken);
    }

    private const string DefaultPromptTemplate =
        "You are an assistant that clusters related dialogue lines for {{projectDisplayName}}.\n" +
        "Return ONLY JSON as an array under key 'clusters' or a bare array of objects with: clusterId, memberIds, sharedContext (optional array), knowledgeReferences (optional array), confidence (0..1), notes (optional).";
}


